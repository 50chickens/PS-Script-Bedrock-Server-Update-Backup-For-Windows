using System.IO.Compression;
using System.Text.Json;
using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles checking for Bedrock server updates, downloading them, and applying patches.
/// Checks once per day for new versions and orchestrates the update process.
/// </summary>
public class MinecraftServerPatchService : IMinecraftServerPatchService
{
    private readonly ILog<MinecraftServerPatchService> _log;
    private readonly IMineCraftUpdateDownloaderService _updateDownloaderService;
    private readonly string _serverPath;
    private MineCraftServerOptions _options;
    private DateTime _lastUpdateCheckTime = DateTime.MinValue;
    private readonly TimeSpan _updateCheckInterval = TimeSpan.FromHours(24);

    public MinecraftServerPatchService(
        ILog<MinecraftServerPatchService> logger,
        IMineCraftUpdateDownloaderService updateDownloaderService,
        MineCraftServerOptions options)
    {
        _log = logger ?? throw new ArgumentNullException(nameof(logger));
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
        _log.Info($"Downloading Minecraft Bedrock server version {version}...");
        
        // First, get the JSON metadata and extract the Windows download URL
        var downloadUrl = await GetWindowsDownloadUrlAsync(_options.MineCraftVersionApiUrl, cancellationToken);
        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new InvalidOperationException("Could not find Windows download URL in API response");
        }
        
        _log.Info($"Download URL: {downloadUrl}");
        
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
            _log.Info($"Removing existing download file: {downloadFileName}");
            File.Delete(downloadFileName);
        }
        
        // Download the actual ZIP file
        await DownloadFileAsync(downloadUrl, downloadFileName, cancellationToken);
        
        _log.Info($"Update downloaded to {downloadFileName}");

        // Extract update over existing installation
        UpdateServerFromZipFile(downloadFileName);
        _log.Info("Update files extracted to server directory");

        _log.Info("Update applied successfully");
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
                            return urlElement.GetString();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to parse download metadata from API");
        }
        
        return null;
    }

    /// <summary>
    /// Downloads a file from the given URL to the specified path.
    /// </summary>
    private async Task DownloadFileAsync(string url, string filePath, CancellationToken cancellationToken)
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

    /// <summary>
    /// Extracts the update ZIP file to the server path.
    /// </summary>
    private void UpdateServerFromZipFile(string zipPath)
    {
        try
        {
            ZipFile.ExtractToDirectory(zipPath, _serverPath, overwriteFiles: true);
        }
        catch (InvalidDataException ex)
        {
            _log.Error(ex, "ZIP file is corrupted or incomplete. This may indicate a failed download.");
            throw;
        }
    }
}
