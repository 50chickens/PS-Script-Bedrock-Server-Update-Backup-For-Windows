using System.IO.Compression;
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
        var downloadFileName = await _updateDownloaderService.DownloadUpdateAsync(_options.MineCraftVersionApiUrl, cancellationToken);
        
        _log.Info($"Update downloaded to {downloadFileName}");

        // Extract update over existing installation
        UpdateServerFromZipFile(downloadFileName);
        _log.Info("Update files extracted to server directory");
        File.Delete(downloadFileName);

        _log.Info("Update applied successfully");
    }

    /// <summary>
    /// Extracts the update ZIP file to the server path.
    /// </summary>
    private void UpdateServerFromZipFile(string zipPath)
    {
        ZipFile.ExtractToDirectory(zipPath, _serverPath, overwriteFiles: true);
    }
}
