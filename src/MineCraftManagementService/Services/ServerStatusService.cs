using MineCraftManagementService.Enums;
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
    private readonly IMineCraftSchedulerService _mineCraftSchedulerService;
    private readonly IServerStatusProvider _statusProvider;
    private readonly int _autoShutdownAfterSeconds;
    private readonly bool _enableAutoStart;
    private bool _checkForUpdates;
    private string? _pendingPatchVersion = null;  // Version pending to be patched
    private List<int> _updateCheckIntervalsSeconds = new List<int>();

    public ServerStatusService(
        ILog<ServerStatusService> log,
        IMineCraftServerService minecraftService,
        MineCraftServerOptions options,
        IMineCraftUpdateService updateCheckService,
        IMineCraftSchedulerService mineCraftSchedulerService,
        IServerStatusProvider statusProvider)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _updateCheckService = updateCheckService ?? throw new ArgumentNullException(nameof(updateCheckService));
        _mineCraftSchedulerService = mineCraftSchedulerService;
        _statusProvider = statusProvider ?? throw new ArgumentNullException(nameof(statusProvider));
        AddAutoShutdownIntervals(options);
        AddUpdateCheckIntervals(options);
        _autoShutdownAfterSeconds = options.AutoShutdownAfterSeconds;
        _enableAutoStart = options.EnableAutoStart;
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

            // Evaluate lifecycle state in order of priority
            // Check "server not running" states FIRST to avoid repeated stop attempts
            if (ShouldBePatched())
            {
                var patchVersion = _pendingPatchVersion;
                _pendingPatchVersion = null;  // Clear pending patch
                _log.Trace($"Determined lifecycle status: ShouldBePatched (version {patchVersion})");
                _mineCraftSchedulerService.SetAutoShutdownTime(DateTime.MinValue); // Clear auto-shutdown time until server restarts
                _mineCraftSchedulerService.SetUpdateCheckTime(DateTime.MinValue); // Clear update check time until server restarts
                return new MineCraftServerLifecycleStatus
                {
                    LifecycleStatus = MineCraftServerStatus.ShouldBePatched,
                    PatchVersion = patchVersion
                };
            }

            if (ShouldBeStarted())
            {
                _log.Trace("Determined lifecycle status: ShouldBeStarted");
                return new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeStarted };
            }

            if (ShouldBeIdle())
            {
                _log.Trace("Determined lifecycle status: ShouldBeIdle");
                // Clear scheduled times when server becomes idle (stopped and not restarting)
                _mineCraftSchedulerService.SetAutoShutdownTime(DateTime.MinValue);
                _mineCraftSchedulerService.SetUpdateCheckTime(DateTime.MinValue);
                return new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeIdle };
            }

            // Only check ShouldBeStopped if server is actually running (avoids repeated stop attempts)
            if (await ShouldBeStopped())
            {
                _log.Trace("Determined lifecycle status: ShouldBeStopped");
                return new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeStopped };
            }

            if (ShouldBeMonitored())
            {
                _log.Trace("Determined lifecycle status: ShouldBeMonitored");
                if (_mineCraftSchedulerService.GetAutoShutdownTime() == DateTime.MinValue && _autoShutdownAfterSeconds > 0)
                {
                    // If auto-shutdown time was never set (e.g. server started before service), set it now
                    SetAutoShutdownSchedule();
                }
                if (_mineCraftSchedulerService.GetUpdateCheckTime() == DateTime.MinValue && _checkForUpdates)
                {
                    // If update check time was never set (e.g. server started before service), set it now
                    SetUpdateSchedule();
                }
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
    /// <summary>
    /// Checks if the server should be stopped based on scheduled operations.
    /// Returns true if server should stop for either auto-shutdown or update availability.
    /// </summary>
    private async Task<bool> ShouldBeStopped()
    {
        var (shouldStop, reason) = await CheckIfServerShouldStop();
        _log.Trace($"ShouldBeStopped evaluated to {shouldStop} due to: {reason}");
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
            // Server is not running, clear the scheduled stop times to prevent repeated stop attempts
            _mineCraftSchedulerService.SetAutoShutdownTime(DateTime.MinValue);
            _mineCraftSchedulerService.SetUpdateCheckTime(DateTime.MinValue);
            return (false, "");
        }
        if (_autoShutdownAfterSeconds > 0 && _mineCraftSchedulerService.IsAutoShutdownTimeSet() && _mineCraftSchedulerService.GetCurrentTime() >= _mineCraftSchedulerService.GetAutoShutdownTime())
        {
            _log.Info($"Scheduled auto-shutdown time reached.");
            _statusProvider.SetShutdownMode(ServerShutDownMode.DenyRestart);
            return (true, "Auto-shutdown time exceed.");
        }
        // First, check if an update check is scheduled and should run
        if (!_checkForUpdates)
        {
            return (false, "Update checks disabled.");
        }
        if (!_mineCraftSchedulerService.IsUpdateCheckTimeSet())
        {
            return (false, "No valid update check time yet.");
        }
        var updateCheckTime = _mineCraftSchedulerService.GetUpdateCheckTime();
        if (_mineCraftSchedulerService.GetCurrentTime() < updateCheckTime)
        {
            return (false, $"Update check not due until {updateCheckTime:yyyy-MM-dd HH:mm:ss.fff}.");
        }
        try
        {
            var now = _mineCraftSchedulerService.GetCurrentTime();
            _log.Info($"Scheduled update check time reached. Checking for updates... (Now: {now:yyyy-MM-dd HH:mm:ss.fff})");
            _mineCraftSchedulerService.SetUpdateCheckTime(now.AddSeconds(_updateCheckIntervalsSeconds.FirstOrDefault())); //reset the next update check time so that even if an exception occurs.
            _log.Info($"Rescheduled next update check for {_mineCraftSchedulerService.GetUpdateCheckTime():yyyy-MM-dd HH:mm:ss.fff}");
            var (updateAvailable, message, newVersion) = await _updateCheckService.NewVersionIsAvailable(_minecraftService.CurrentVersion);
            _log.Trace($"Update check complete. Available: {updateAvailable}, Message: {message}, NewVersion: {newVersion}");

            if (!updateAvailable)
            {
                return (false, "No update available.");
            }
            else
            {
                _pendingPatchVersion = newVersion;  // Store for later patching
                return (true, "Update available for patching");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception in CheckIfServerShouldStop while checking for updates");
            return (false, $"Exception {ex.Message} during update check.");
        }

    }
    private void LogScheduledOperationStatus()
    {
        var now = _mineCraftSchedulerService.GetCurrentTime();

        // Log time until next auto-shutdown
        if (_autoShutdownAfterSeconds > 0)
        {
            if (_mineCraftSchedulerService.IsAutoShutdownTimeSet())
            {
                var autoShutdownTime = _mineCraftSchedulerService.GetAutoShutdownTime();
                var remainingSeconds = (int)(autoShutdownTime - now).TotalSeconds;
                if (remainingSeconds > 0)
                {
                    _log.Trace($"Auto-shutdown in {remainingSeconds} seconds.");
                }
            }
        }

        // Log time until next update check
        if (_checkForUpdates)
        {
            if (_mineCraftSchedulerService.IsUpdateCheckTimeSet())
            {
                var updateCheckTime = _mineCraftSchedulerService.GetUpdateCheckTime();
                var remainingSeconds = (int)(updateCheckTime - now).TotalSeconds;
                if (remainingSeconds > 0)
                {
                    _log.Trace($"Next update check in {remainingSeconds} seconds.");
                }
                else
                {
                    _log.Debug($"Update check is due now.");
                }
            }
        }
    }
    private void AddAutoShutdownIntervals(MineCraftServerOptions options)
    {
        if (options.AutoShutdownAfterSeconds > 0)
        {
            _log.Info($"Auto-shutdown after seconds is set to {options.AutoShutdownAfterSeconds} seconds.");
        }
    }
    private void AddUpdateCheckIntervals(MineCraftServerOptions options)
    {
        if (options.UpdateCheckIntervalSeconds == 0)
        {
            _log.Info("Update check interval is set to 0 seconds. Update checks will be disabled.");
            return;
        }
        if (options.MinimumServerUptimeForUpdateSeconds > 0)
        {
            _log.Info($"Minimum server uptime for first update check is set to {options.MinimumServerUptimeForUpdateSeconds} seconds.");
            _updateCheckIntervalsSeconds.Add(options.MinimumServerUptimeForUpdateSeconds);
        }
        if (options.UpdateCheckIntervalSeconds > 0)
        {
            _log.Info($"Update check interval is set to {options.UpdateCheckIntervalSeconds} seconds.");
            _updateCheckIntervalsSeconds.Add(options.UpdateCheckIntervalSeconds);
        }
    }
    private void SetUpdateSchedule()
    {
        var serviceStartedAt = _mineCraftSchedulerService.GetCurrentTime();
        _mineCraftSchedulerService.SetServiceStartedAt(serviceStartedAt);
        _log.Info($"Service started at {serviceStartedAt:yyyy-MM-dd HH:mm:ss.fff}.");

        var updateCheckIntervalSeconds = _updateCheckIntervalsSeconds.FirstOrDefault();
        if (updateCheckIntervalSeconds == 0)
        {
            _log.Info("Update check interval set to 0. Skipping setting schedule for next update check.");
            return;
        }
        var updateCheckTime = serviceStartedAt.AddSeconds(updateCheckIntervalSeconds);
        _mineCraftSchedulerService.SetUpdateCheckTime(updateCheckTime);
        _log.Info($"Scheduling next update check for {updateCheckTime:yyyy-MM-dd HH:mm:ss.fff}.");
        if (_updateCheckIntervalsSeconds.Count > 1) //keep the last item in the list. it will be used for subsequent checks.
        {
            _updateCheckIntervalsSeconds.Remove(updateCheckIntervalSeconds);
            _log.Debug($"Removing used update check interval of {updateCheckIntervalSeconds} seconds from the list.");
        }
    }
    private void SetAutoShutdownSchedule()
    {
        var now = _mineCraftSchedulerService.GetCurrentTime();
        _mineCraftSchedulerService.SetServiceStartedAt(now);
        var autoShutdownTime = now.AddSeconds(_autoShutdownAfterSeconds);
        _mineCraftSchedulerService.SetAutoShutdownTime(autoShutdownTime);
        _log.Info($"Setting Auto-shutdown to {autoShutdownTime:yyyy-MM-dd HH:mm:ss.fff}.");
    }

}