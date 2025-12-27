using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Determines the server lifecycle status based on server state, version, and runtime.
/// </summary>
public partial class ServerStatusService : IServerStatusService
{
    private readonly ILog<ServerStatusService> _log;
    private readonly IMineCraftServerService _minecraftService;
    private readonly IMineCraftUpdateService _updateCheckService;

    private readonly int _updateCheckIntervalSeconds;
    private readonly int _autoShutdownAfterSeconds;
    private readonly bool _enableAutoStart;
    private readonly int _minimumServerUptimeForUpdateSeconds;
    private bool _checkForUpdates;
    private string? _pendingPatchVersion = null;  // Version pending to be patched
    
    // Scheduler: tracks when each operation should next run
    private DateTime _nextUpdateCheckTime = DateTime.MinValue;
    private DateTime _nextAutoShutdownTime = DateTime.MinValue;
    private DateTime _lastServerStartTime = DateTime.MinValue;
    
    public ServerStatusService(
        ILog<ServerStatusService> logger,
        IMineCraftServerService minecraftService,
        MineCraftServerOptions options,
        IMineCraftUpdateService updateCheckService)
    {
        _log = logger ?? throw new ArgumentNullException(nameof(logger));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _updateCheckService = updateCheckService ?? throw new ArgumentNullException(nameof(updateCheckService));
        _updateCheckIntervalSeconds = options.UpdateCheckIntervalSeconds;
        _autoShutdownAfterSeconds = options.AutoShutdownAfterSeconds;
        _enableAutoStart = options.EnableAutoStart;
        _minimumServerUptimeForUpdateSeconds = options.MinimumServerUptimeForUpdateSeconds;
        _checkForUpdates = options.CheckForUpdates;
    }

    /// <summary>
    /// Determines the current server lifecycle status based on server state, version, and runtime.
    /// Uses a scheduler-based approach: each operation (monitoring, update checks, auto-shutdown) has a scheduled time.
    /// Operations are only performed when current time reaches their scheduled time.
    /// </summary>
    public async Task<MineCraftServerLifecycleStatus> GetLifeCycleStateAsync()
    {
        try
        {
            _log.Info("Evaluating server lifecycle status...");

            // Handle server start time reset (when server transitions from stopped to running)
            if (ShouldResetUpdateTimer())
            {
                _log.Debug("Server start time changed, timers reset as needed.");
                ResetUpdateTimer();
                ResetAutoShutdownTimer();
            }

            // Evaluate lifecycle state in order of priority
            if (await ShouldBeStopped())
            {
                _log.Debug("Determined lifecycle status: ShouldBeStopped");
                return new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeStopped };
            }

            if (ShouldBePatched())
            {
                var patchVersion = _pendingPatchVersion;
                _pendingPatchVersion = null;  // Clear pending patch
                _log.Debug($"Determined lifecycle status: ShouldBePatched (version {patchVersion})");
                return new MineCraftServerLifecycleStatus
                {
                    LifecycleStatus = MineCraftServerStatus.ShouldBePatched,
                    PatchVersion = patchVersion
                };
            }

            if (ShouldBeIdleCooldown())
            {
                _log.Debug($"Determined lifecycle status: ShouldBeIdle (auto-shutdown cooldown)");
                return new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeIdle };
            }

            if (ShouldBeStarted())
            {
                _log.Debug("Determined lifecycle status: ShouldBeStarted");
                return new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeStarted };
            }

            if (ShouldBeIdle())
            {
                _log.Debug("Determined lifecycle status: ShouldBeIdle");
                return new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeIdle };
            }

            if (ShouldBeMonitored())
            {
                _log.Debug("Determined lifecycle status: ShouldBeMonitored");
                return new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeMonitored };
            }
            _log.Error("No applicable lifecycle status found.");
            // Fallback - should not reach here
            throw new InvalidOperationException("Unable to determine server lifecycle status.");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception in GetLifeCycleStateAsync");
            throw;
        }
    }

    private void ResetUpdateTimer()
    {
        
        _lastServerStartTime = _minecraftService.ServerStartTime;

        // Only reset update check time on initial start, not after restarts
        if (_nextUpdateCheckTime != DateTime.MinValue)
        {
            _log.Debug($"Server restarted. Next update check scheduled for {_nextUpdateCheckTime:yyyy-MM-dd HH:mm:ss.fff}");
        }
        else
        {
            _nextUpdateCheckTime = DateTime.Now.AddSeconds(_minimumServerUptimeForUpdateSeconds);
            _log.Debug($"Server started. Scheduled first update check at {_nextUpdateCheckTime:yyyy-MM-dd HH:mm:ss.fff}");
        }
    }
    private void ResetAutoShutdownTimer()
    {       
        _nextAutoShutdownTime = DateTime.Now.AddSeconds(_autoShutdownAfterSeconds);
        _log.Debug($"Auto-shutdown timer reset. Next auto-shutdown scheduled for {_nextAutoShutdownTime:yyyy-MM-dd HH:mm:ss.fff}");        
    }
    private bool ShouldResetUpdateTimer()
    {
        return _minecraftService.IsRunning && _lastServerStartTime != _minecraftService.ServerStartTime;
    }

    /// <summary>
    /// Checks if the server should be stopped based on scheduled operations.
    /// Returns true if server should stop for either auto-shutdown or update availability.
    /// </summary>
    private async Task<bool> ShouldBeStopped()
    {
        var (shouldStop, _) = await CheckIfServerShouldStop();
        return shouldStop;
    }

    /// <summary>
    /// Checks if server is stopped and has a pending patch waiting to be applied.
    /// </summary>
    private bool ShouldBePatched()
    {
        return !_minecraftService.IsRunning && _pendingPatchVersion != null;
    }

    /// <summary>
    /// Checks if server is stopped and within auto-shutdown cooldown period.
    /// When auto-shutdown triggers, the next restart is delayed by the auto-shutdown duration.
    /// </summary>
    private bool ShouldBeIdleCooldown()
    {
        return !_minecraftService.IsRunning && _autoShutdownAfterSeconds > 0 && DateTime.Now < _nextAutoShutdownTime;
    }

    /// <summary>
    /// Checks if server should be started (server not running and auto-start is enabled).
    /// </summary>
    private bool ShouldBeStarted()
    {
        return !_minecraftService.IsRunning && _enableAutoStart;
    }

    /// <summary>
    /// Checks if server should be idle (server not running and auto-start is disabled).
    /// </summary>
    private bool ShouldBeIdle()
    {
        return !_minecraftService.IsRunning && !_enableAutoStart;
    }

    /// <summary>
    /// Checks if server should be monitored (server is running and up-to-date).
    /// Logs remaining time to next scheduled operations.
    /// </summary>
    private bool ShouldBeMonitored()
    {
        if (_minecraftService.IsRunning)
        {
            LogScheduledOperationStatus();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if the server should be stopped based on scheduled operations.
    /// Returns tuple: (shouldStop, reason).
    /// Uses scheduler-based approach: checks if current time has reached scheduled stop times.
    /// First update check is scheduled at MinimumServerUptimeForUpdateSeconds.
    /// Subsequent update checks are scheduled at UpdateCheckIntervalSeconds intervals (24 hours).
    /// </summary>
    internal async Task<(bool shouldStop, string reason)> CheckIfServerShouldStop()
    {
        if (!_minecraftService.IsRunning)
        {
            return (false, "");
        }

        // First, check if an update check is scheduled and should run
        if (_checkForUpdates && DateTime.Now >= _nextUpdateCheckTime)
        {
            try
            {
                _log.Debug($"Scheduled update check time reached. Checking for updates... (Now: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff})");
                var (updateAvailable, message, newVersion) = await _updateCheckService.NewVersionIsAvailable(_minecraftService.CurrentVersion);
                _log.Debug($"Update check complete. Available: {updateAvailable}, Message: {message}, NewVersion: {newVersion}");
                
                if (updateAvailable)
                {
                    _log.Info($"Update available: current version {_minecraftService.CurrentVersion} → {newVersion}. Server will be stopped for patching.");
                    _pendingPatchVersion = newVersion;  // Store for later patching
                    // Reschedule next check for 24 hours from now
                    _nextUpdateCheckTime = DateTime.Now.AddSeconds(_updateCheckIntervalSeconds);
                    _log.Debug($"Rescheduled next update check for {_nextUpdateCheckTime:yyyy-MM-dd HH:mm:ss.fff} (24 hours from now)");
                    return (true, "update available for patching");
                }
                
                // Mark that first check has been performed and reschedule for 24 hours
                _nextUpdateCheckTime = DateTime.Now.AddSeconds(_updateCheckIntervalSeconds);
                _log.Debug($"No update available. Rescheduled next update check for {_nextUpdateCheckTime:yyyy-MM-dd HH:mm:ss.fff} (24 hours from now)");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception in CheckIfServerShouldStop while checking for updates");
                // Reschedule even on error so we don't get stuck
                _nextUpdateCheckTime = DateTime.Now.AddSeconds(_updateCheckIntervalSeconds);
            }
        }

        // Second, check if auto-shutdown is scheduled and should run
        if (_autoShutdownAfterSeconds > 0 && DateTime.Now >= _nextAutoShutdownTime)
        {
            _log.Info($"Scheduled auto-shutdown time reached.");
            _nextAutoShutdownTime = DateTime.Now.AddSeconds(_autoShutdownAfterSeconds);
            return (true, "auto-shutdown timer reached");
        }

        return (false, "");
    }

    /// <summary>
    /// Checks if server should be idle (server not running and auto-start is disabled).
    /// </summary>
    internal MineCraftServerStatus ShouldBeStartedOrIdle()
    {
        // If auto-start is disabled, go to idle instead of starting
        return _enableAutoStart ? MineCraftServerStatus.ShouldBeStarted : MineCraftServerStatus.ShouldBeIdle;
    }

    private void LogScheduledOperationStatus()
    {
        var now = DateTime.Now;
        
        // Log time until next auto-shutdown
        if (_autoShutdownAfterSeconds > 0)
        {
            var remainingSeconds = (int)(_nextAutoShutdownTime - now).TotalSeconds;
            if (remainingSeconds > 0)
            {
                _log.Info($"Auto-shutdown in {remainingSeconds} seconds.");
            }
        }

        // Log time until next update check
        if (_checkForUpdates)
        {
            var remainingSeconds = (int)(_nextUpdateCheckTime - now).TotalSeconds;
            if (remainingSeconds > 0)
            {
                _log.Debug($"Next update check in {remainingSeconds} seconds.");
            }
            else
            {
                _log.Debug($"Update check is due now.");
            }
        }
    }

    /// <summary>
    /// Reschedules the next update check to run after the specified interval.
    /// Called after a patch is successfully applied to set the next check time.
    /// </summary>
    public void RescheduleNextUpdateCheck(int updateCheckIntervalSeconds)
    {
        _nextUpdateCheckTime = DateTime.Now.AddSeconds(updateCheckIntervalSeconds);
        _log.Debug($"Rescheduled next update check for {_nextUpdateCheckTime:yyyy-MM-dd HH:mm:ss.fff} ({updateCheckIntervalSeconds} seconds from now)");
    }
}