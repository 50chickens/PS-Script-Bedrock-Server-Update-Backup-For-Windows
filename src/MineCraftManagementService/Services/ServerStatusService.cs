using MineCraftManagementService.Extensions;
using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Determines the server status based on server state, version, and runtime.
/// </summary>
public partial class ServerStatusService : IServerStatusService
{
    private readonly ILog<ServerStatusService> _log;
    private readonly IMineCraftServerService _minecraftService;
    private readonly IMineCraftUpdateService _updateCheckService;

    private readonly int _updateCheckIntervalMs;
    private readonly int _autoShutdownAfterSeconds;
    private readonly bool _enableAutoStart;
    private readonly int _minimumServerUptimeForUpdateSeconds;
    private bool _checkForUpdates;
    private DateTime _lastUpdateCheckTime = DateTime.MinValue;
    
    public ServerStatusService(
        ILog<ServerStatusService> logger,
        IMineCraftServerService minecraftService,
        MineCraftServerOptions options,
        IMineCraftUpdateService updateCheckService)
    {
        _log = logger ?? throw new ArgumentNullException(nameof(logger));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _updateCheckService = updateCheckService ?? throw new ArgumentNullException(nameof(updateCheckService));
        _updateCheckIntervalMs = options.UpdateCheckIntervalSeconds * 1000;
        _autoShutdownAfterSeconds = options.AutoShutdownAfterSeconds;
        _enableAutoStart = options.EnableAutoStart;
        _minimumServerUptimeForUpdateSeconds = options.MinimumServerUptimeForUpdateSeconds;
        _checkForUpdates = options.CheckForUpdates;
    }

    /// <summary>
    /// Determines the current server status based on server state, version, and runtime.
    /// Implements periodic update checking: every UpdateCheckIntervalSeconds, checks for available patches.
    /// If a patch is available while monitoring a running server, transitions to ShouldBeStopped state.
    /// If EnableAutoStart is false and server is not running, returns ShouldBeIdle instead of ShouldBeStarted.
    /// Logs auto-shutdown remaining time when server is being monitored with auto-shutdown configured.
    /// </summary>
    public async Task<MineCraftServerStatus> GetStatusAsync()
    {
        if (ShouldBeStopped())
        {
            return MineCraftServerStatus.ShouldBeStopped;
        }

        if (await ShouldBeStoppedForUpdate())
        {
            return MineCraftServerStatus.ShouldBeStopped;
        }

        if (await ShouldBePatched())
        {
            return MineCraftServerStatus.ShouldBePatched;
        }

        if (!_minecraftService.IsRunning)
        {
            return ShouldBeStartedOrIdle();
        }

        // Server is running and up-to-date, log auto-shutdown remaining time if configured
        LogAutoShutdownRemainingTime();
        return MineCraftServerStatus.ShouldBeMonitored;
    }

    internal bool ShouldBeStopped()
    {
        // Check if auto-shutdown time exceeded
        if (_minecraftService.ServerStartTime.HasExceededAutoShutdownTime(_autoShutdownAfterSeconds, out _))
        {
            // If server is running, stop it
            if (_minecraftService.IsRunning)
            {
                return true;
            }
        }

        return false;
    }

    internal async Task<bool> ShouldBeStoppedForUpdate()
    {
        // Check if update is available and server is running - need to stop for patching
        // Always check for updates, but only stop if minimum uptime has been reached
        if (_checkForUpdates && _minecraftService.IsRunning &&
            DateTime.Now - _lastUpdateCheckTime >= TimeSpan.FromMilliseconds(_updateCheckIntervalMs))
        {
            var (updateAvailable, _, newVersion) = await _updateCheckService.NewVersionIsAvailable(_minecraftService.CurrentVersion);
            if (updateAvailable)
            {
                if (HasMinimumIdleTime())
                {
                    _log.Info($"Update available: current version {_minecraftService.CurrentVersion} → {newVersion}");
                    return true; // Stop server so it can be patched (don't update _lastUpdateCheckTime so ShouldBePatched can check immediately)
                }
                else
                {
                    _log.Info($"Update available but server hasn't been idle long enough. Minimum uptime required: {_minimumServerUptimeForUpdateSeconds} seconds");
                }
            }
            else
            {
                // No update available, update the timestamp to avoid checking too frequently
                _lastUpdateCheckTime = DateTime.Now;
            }
        }

        return false;
    }

    internal async Task<bool> ShouldBePatched()
    {
        // Check for available patches when server is stopped
        // Note: if server is running, ShouldBeStoppedForUpdate will have already checked and returned ShouldBeStopped
        // Minimum uptime requirement was already validated in ShouldBeStoppedForUpdate before stopping the server
        if (_checkForUpdates && !_minecraftService.IsRunning && 
            DateTime.Now - _lastUpdateCheckTime >= TimeSpan.FromMilliseconds(_updateCheckIntervalMs))
        {
            _lastUpdateCheckTime = DateTime.Now;
            
            var (updateAvailable, _, newVersion) = await _updateCheckService.NewVersionIsAvailable(_minecraftService.CurrentVersion);
            if (updateAvailable)
            {
                _log.Info($"Update available: current version {_minecraftService.CurrentVersion} → {newVersion}");
                return true; // Patch the stopped server
            }
        }

        return false;
    }

    internal bool HasMinimumIdleTime()
    {
        // Check if server has been up for minimum time before allowing patch
        // If server was never started (MinValue), it's not eligible for patching
        if (_minecraftService.ServerStartTime == DateTime.MinValue)
        {
            return false;
        }

        var uptime = DateTime.Now - _minecraftService.ServerStartTime;
        return uptime.TotalSeconds >= _minimumServerUptimeForUpdateSeconds;
    }

    internal MineCraftServerStatus ShouldBeStartedOrIdle()
    {
        // If auto-start is disabled, go to idle instead of starting
        return _enableAutoStart ? MineCraftServerStatus.ShouldBeStarted : MineCraftServerStatus.ShouldBeIdle;
    }

    private void LogAutoShutdownRemainingTime()
    {
        if (_autoShutdownAfterSeconds <= 0 || !_minecraftService.IsRunning)
        {
            return;
        }

        var serverStartTime = _minecraftService.ServerStartTime;
        if (serverStartTime == DateTime.MinValue)
        {
            return;
        }

        var elapsed = DateTime.Now - serverStartTime;
        var remaining = _autoShutdownAfterSeconds - (int)elapsed.TotalSeconds;
        if (remaining > 0)
        {
            _log.Info($"Auto-shutdown in {remaining} seconds.");
        }
    }
}