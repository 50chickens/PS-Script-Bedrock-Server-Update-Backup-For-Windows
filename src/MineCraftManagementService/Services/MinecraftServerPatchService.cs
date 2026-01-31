using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using System.IO.Compression;
using System.Text.Json;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles checking for Bedrock server updates, downloading them, and applying patches.
/// Checks once per day for new versions and orchestrates the update process.
/// </summary>
public class MinecraftServerPatchService : IMinecraftServerPatchService
{
    private readonly ILog<MinecraftServerPatchService> _log;
    private readonly IMineCraftUpdateDownloadService _updateDownloaderService;
    private readonly string _serverPath;
    private MineCraftServerOptions _options;
    private DateTime _lastUpdateCheckTime = DateTime.MinValue;
    private readonly TimeSpan _updateCheckInterval = TimeSpan.FromHours(24);

    public MinecraftServerPatchService(
        ILog<MinecraftServerPatchService> log,
        IMineCraftUpdateDownloadService updateDownloaderService,
        MineCraftServerOptions options)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _updateDownloaderService = updateDownloaderService ?? throw new ArgumentNullException(nameof(updateDownloaderService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serverPath = _options.ServerPath;
    }


    /// <summary>
    /// Downloads and applies the update by replacing files.
    /// Caller is responsible for stopping the server before, creating backup, and starting it after the update.
    /// </summary>
    public async Task ApplyUpdateAsync(string version, CancellationToken cancellationToken)
    {
        _log.Info($"Applying Minecraft Bedrock server update to version {version}");

        // First, get the JSON metadata and extract the Windows download URL
        var downloadUrl = await GetWindowsDownloadUrlAsync(_options.MineCraftVersionApiUrl, cancellationToken);
        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new InvalidOperationException($"Could not find Windows download URL in API response for version {version}");
        }

        // Build the final filename with version suffix
        var downloadFolder = _options.DownloadFolderName;
        if (string.IsNullOrEmpty(downloadFolder))
        {
            downloadFolder = _options.BackupFolderName;
        }

        // Create download folder if it doesn't exist
        Directory.CreateDirectory(downloadFolder);

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_options.DownloadFileName);
        var downloadFileName = Path.Combine(downloadFolder, $"{fileNameWithoutExtension}-{version}.zip");

        // Remove existing file if configured
        if (_options.OverwriteExistingDownloadFile && File.Exists(downloadFileName))
        {
            File.Delete(downloadFileName);
        }

        // Download the actual ZIP file
        await DownloadFileAsync(downloadUrl, downloadFileName, cancellationToken);

        // Extract update over existing installation
        UpdateServerFromZipFile(downloadFileName);
        
        _log.Info($"Update to version {version} applied successfully");
    }

    /// <summary>
    /// Extracts the JSON metadata response and gets the Windows Bedrock server download URL.
    /// </summary>
    private async Task<string?> GetWindowsDownloadUrlAsync(string metadataUrl, CancellationToken cancellationToken)
    {
        try
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(_options.DownloadTimeoutSeconds);
                var response = await httpClient.GetStringAsync(metadataUrl, cancellationToken);

                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                if (root.TryGetProperty("result", out var resultElement) &&
                    resultElement.TryGetProperty("links", out var linksArray))
                {
                    foreach (var link in linksArray.EnumerateArray())
                    {
                        if (link.TryGetProperty("downloadType", out var typeElement) &&
                            typeElement.GetString() == "serverBedrockWindows" &&
                            link.TryGetProperty("downloadUrl", out var urlElement))
                        {
                            var downloadUrl = urlElement.GetString();
                            if (!string.IsNullOrEmpty(downloadUrl))
                            {
                                return downloadUrl;
                            }
                        }
                    }
                }

                _log.Error($"Windows download URL not found in API response. Metadata URL: {metadataUrl}");
                return null;
            }
        }
        catch (JsonException ex)
        {
            _log.Error(ex, $"Failed to parse JSON from download metadata API. URL: {metadataUrl}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _log.Error(ex, $"HTTP request failed when fetching download metadata. URL: {metadataUrl}");
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _log.Error(ex, $"Request timed out after {_options.DownloadTimeoutSeconds}s when fetching metadata. URL: {metadataUrl}");
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _log.Warn("Metadata download was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Unexpected error fetching download metadata. URL: {metadataUrl}");
            return null;
        }
    }

    /// <summary>
    /// Downloads a file from the given URL to the specified path.
    /// </summary>
    private async Task DownloadFileAsync(string url, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(_options.DownloadTimeoutSeconds);
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = File.Create(filePath);
                // Use larger buffer for faster copying
                await contentStream.CopyToAsync(fileStream, 1024 * 1024, cancellationToken);  // 1MB buffer
            }
        }
        catch (HttpRequestException ex)
        {
            _log.Error(ex, $"HTTP request failed downloading file. URL: {url}, Destination: {filePath}");
            throw;
        }
        catch (IOException ex)
        {
            _log.Error(ex, $"I/O error writing downloaded file. Path: {filePath}");
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _log.Warn($"File download cancelled. URL: {url}");
            throw;
        }
    }

    /// <summary>
    /// Extracts the update ZIP file to the server path.
    /// </summary>
    private void UpdateServerFromZipFile(string zipPath)
    {
        try
        {
            _log.Info($"Extracting update from {Path.GetFileName(zipPath)} to server directory");
            ZipFile.ExtractToDirectory(zipPath, _serverPath, overwriteFiles: true);
        }
        catch (InvalidDataException ex)
        {
            _log.Error(ex, $"ZIP file is corrupted or incomplete. Path: {zipPath}. This may indicate a failed download.");
            throw;
        }
        catch (IOException ex)
        {
            _log.Error(ex, $"I/O error extracting ZIP file. Source: {zipPath}, Destination: {_serverPath}");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.Error(ex, $"Access denied extracting ZIP file. Destination: {_serverPath}");
            throw;
        }
    }
}
