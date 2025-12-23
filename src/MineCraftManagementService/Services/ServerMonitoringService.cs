using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Monitors the health and status of the Minecraft server at regular intervals.
/// Also orchestrates periodic update checks and applies patches when updates are available.
/// </summary>
public class ServerMonitoringService
{
    private readonly ILog<ServerMonitoringService> _log;
    private readonly MineCraftServerService _minecraftService;
    private readonly MineCraftUpdateService _updateCheckService;
    private readonly MinecraftServerPatchService _patchService;

    private readonly int _monitoringIntervalMs;
    private readonly int _updateCheckIntervalMs;
    private readonly int _autoShutdownAfterSeconds;
    private bool _checkForUpdates;
    private DateTime _lastUpdateCheckTime = DateTime.MinValue;

    public ServerMonitoringService(
        ILog<ServerMonitoringService> logger,
        MineCraftServerService minecraftService,
        MineCraftServerOptions options,
        MineCraftUpdateService updateCheckService,
        MinecraftServerPatchService patchService)
    {
        _log = logger ?? throw new ArgumentNullException(nameof(logger));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _updateCheckService = updateCheckService ?? throw new ArgumentNullException(nameof(updateCheckService));
        _patchService = patchService ?? throw new ArgumentNullException(nameof(patchService));
        _monitoringIntervalMs = options.MonitoringIntervalSeconds * 1000;
        _updateCheckIntervalMs = options.UpdateCheckIntervalSeconds * 1000;
        _autoShutdownAfterSeconds = options.AutoShutdownAfterSeconds;
        _checkForUpdates = options.CheckForUpdates;
    }

    /// <summary>
    /// Continuously monitors the server status and checks for updates until cancellation is requested.
    /// When an update is found, initiates the patching process.
    /// </summary>
    public async Task MonitorServerAsync(CancellationToken cancellationToken)
    {
        bool havePrintedUpdateCheckMessage = false;
        DateTime serverStartTime = _minecraftService.ServerStartTime;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            if (serverStartTime.HasExceededAutoShutdownTime(_autoShutdownAfterSeconds, out int secondsRemaining))
            {
                _log.Info($"Auto-shutdown time of {_autoShutdownAfterSeconds} seconds exceeded. Stopping server...");
                var wasShutdownGracefully = await _minecraftService.TryGracefulShutdownAsync();
                if (!wasShutdownGracefully)
                {
                    _log.Warn("Graceful shutdown failed, forcing server stop");
                    await _minecraftService.ForceStopServerAsync();
                }
                return;
            }
            
            // Only log remaining time if auto-shutdown is enabled
            if (_autoShutdownAfterSeconds > 0)
            {
                _log.Info($"Auto-shutdown in {secondsRemaining} seconds.");
            }
            
            var status = _minecraftService.GetStatus();
            _log.Trace($"Server status: {status}");

            if (DateTime.UtcNow - _lastUpdateCheckTime >= TimeSpan.FromMilliseconds(_updateCheckIntervalMs))
            {
                _lastUpdateCheckTime = DateTime.UtcNow;
            }

            await Task.Delay(_monitoringIntervalMs, cancellationToken);
            // Check for updates periodically
            if (!havePrintedUpdateCheckMessage)
            {
                havePrintedUpdateCheckMessage = true;
                if (_checkForUpdates)
                {
                    _log.Debug("Running periodic update check...");
                }
                else
                {
                    _log.Debug("Periodic update check is disabled.");
                }
            }
            
            if (_checkForUpdates)
            {
                var (updateAvailable, message, newVersion) = await _updateCheckService.NewVersionIsAvailable(cancellationToken);
                if (updateAvailable)
                {
                    _log.Info($"Update available: {message}. Starting patch process...");
                    await _patchService.ApplyUpdateAsync(newVersion, cancellationToken);
                    _log.Info("Update applied successfully");
                }
            }
        }
        if (cancellationToken.IsCancellationRequested)
        {
            _log.Info("Server monitoring cancelled");
            //shutdown the minecraft server if running
            if (_minecraftService.GetStatus() == MineCraftServerStatus.Running)
            {
                _log.Info("Stopping Minecraft server due to monitoring cancellation...");
                var wasShutdownGracefully = await _minecraftService.TryGracefulShutdownAsync();
                if (!wasShutdownGracefully)
                {
                    _log.Warn("Graceful shutdown failed, forcing server stop");
                    await _minecraftService.ForceStopServerAsync();
                }
            }
        }
    }
}

public static class MineCraftServerServiceExtensions
{
    //checks to see if the server has been running more than the specified auto-shutdown time
    public static bool HasExceededAutoShutdownTime(this DateTime serverStartTime, int autoShutdownAfterSeconds, out int secondsRemaining)
    {
        secondsRemaining = 0;
        if (autoShutdownAfterSeconds <= 0)
            return false;

        var runDuration = DateTime.UtcNow - serverStartTime;
        secondsRemaining = autoShutdownAfterSeconds - (int)runDuration.TotalSeconds;
        return runDuration.TotalSeconds >= autoShutdownAfterSeconds;
    }
}

