using System.IO.Compression;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles checking for Bedrock server updates, downloading them, and applying patches.
/// Checks once per day for new versions and orchestrates the update process.
/// </summary>
public class MinecraftServerPatchService
{
    private readonly ILog<MinecraftServerPatchService> _log;
    private readonly MineCraftServerService _minecraftService;
    private readonly MineCraftUpdateDownloaderService _updateDownloaderService;
    private readonly MineCraftBackupService _backupService;
    private readonly string _serverPath;
    private MineCraftServerOptions _options;
    private DateTime _lastUpdateCheckTime = DateTime.MinValue;
    private readonly TimeSpan _updateCheckInterval = TimeSpan.FromHours(24);

    public MinecraftServerPatchService(
        ILog<MinecraftServerPatchService> logger,
        MineCraftServerService minecraftService,
        MineCraftUpdateDownloaderService updateDownloaderService,
        MineCraftBackupService backupService,
        MineCraftServerOptions options)
    {
        _log = logger ?? throw new ArgumentNullException(nameof(logger));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _updateDownloaderService = updateDownloaderService ?? throw new ArgumentNullException(nameof(updateDownloaderService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serverPath = _options.ServerPath;
    }


    /// <summary>
    /// Downloads and applies the update by backing up and replacing files.
    /// Restarts the server after applying the update.
    /// </summary>
    public async Task ApplyUpdateAsync(string version, CancellationToken cancellationToken)
    {
        if (_minecraftService.GetStatus() == MineCraftServerStatus.Running)
        {      
            _log.Info("Server is running - stopping server before applying update");
            var wasShutdownGracefully = await _minecraftService.TryGracefulShutdownAsync();
            if (!wasShutdownGracefully)
            {
                _log.Warn("Graceful shutdown failed, forcing server stop");
                await _minecraftService.ForceStopServerAsync();
            }
        }
        _log.Info($"Downloading Minecraft Bedrock server version {version}...");
        var downloadFileName = await _updateDownloaderService.DownloadUpdateAsync(_options.MineCraftVersionApiUrl, cancellationToken);
        
        _log.Info($"Update downloaded to {downloadFileName}");

        // Create backup of current installation
        var backupPath = _backupService.CreateBackupZipFromServerFolder();
        _log.Info($"Backed up current installation to {backupPath}");

        // Extract update over existing installation
        UpdateServerFromZipFile(downloadFileName);
        _log.Info("Update files extracted to server directory");
        File.Delete(downloadFileName);

        // Restart the server after update
        _log.Info("Update applied successfully. Restarting server...");
        await _minecraftService.StartServerAsync();
        
    }

    /// <summary>
    /// Extracts the update ZIP file to the server path.
    /// </summary>
    private void UpdateServerFromZipFile(string zipPath)
    {
        ZipFile.ExtractToDirectory(zipPath, _serverPath, overwriteFiles: true);
    }
}
