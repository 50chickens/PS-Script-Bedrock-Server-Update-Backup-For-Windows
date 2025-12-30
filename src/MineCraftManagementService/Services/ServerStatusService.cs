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
    private readonly IServerStatusProvider _statusProvider;
    private readonly int _autoShutdownAfterSeconds;
    private readonly bool _enableAutoStart;
    private readonly int _minimumServerUptimeForUpdateSeconds;
    private DateTime _serviceStartedAt;
    private bool _checkForUpdates;
    private string? _pendingPatchVersion = null;  // Version pending to be patched
    private DateTime _updateCheckTime = DateTime.MinValue;
    private List<int> _updateCheckIntervalsSeconds = new List<int>();
    
    private DateTime _autoShutdownTime = DateTime.MinValue;
    
    public ServerStatusService(
        ILog<ServerStatusService> log,
        IMineCraftServerService minecraftService,
        MineCraftServerOptions options,
        IMineCraftUpdateService updateCheckService,
        IServerStatusProvider statusProvider)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _updateCheckService = updateCheckService ?? throw new ArgumentNullException(nameof(updateCheckService));
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
                _autoShutdownTime = DateTime.MinValue; // Clear auto-shutdown time until server restarts
                _updateCheckTime = DateTime.MinValue; // Clear update check time until server restarts
                return new MineCraftServerLifecycleStatus
                {
                    LifecycleStatus = MineCraftServerStatus.ShouldBePatched,
                    PatchVersion = patchVersion
                };
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
                if (_autoShutdownTime == DateTime.MinValue)
                {
                    // If auto-shutdown time was never set (e.g. server started before service), set it now
                    SetAutoShutdownSchedule();
                }
                if (_updateCheckTime == DateTime.MinValue)
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
        _log.Debug($"ShouldBeStopped evaluated to {shouldStop} due to: {reason}");
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
            return (false, "");
        }
        if (_autoShutdownAfterSeconds > 0 && _autoShutdownTime != DateTime.MinValue && DateTime.Now >= _autoShutdownTime)
        {
            _log.Debug($"Scheduled auto-shutdown time reached.");
            _statusProvider.SetShutdownMode(ServerShutDownMode.DenyRestart);
            return (true, "Auto-shutdown time exceed.");
        }
        // First, check if an update check is scheduled and should run
        if (!_checkForUpdates)
        {
            return (false, "Update checks disabled.");
        }
        if (_updateCheckTime == DateTime.MinValue)
        {
            return (false, "No valid update check time yet.");
        }
        if (DateTime.Now <  _updateCheckTime)
        {
            return (false, $"Update check not due until {_updateCheckTime:yyyy-MM-dd HH:mm:ss.fff}.");
        }
        try
        {    
            _log.Debug($"Scheduled update check time reached. Checking for updates... (Now: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff})");
            var (updateAvailable, message, newVersion) = await _updateCheckService.NewVersionIsAvailable(_minecraftService.CurrentVersion);
            _log.Debug($"Update check complete. Available: {updateAvailable}, Message: {message}, NewVersion: {newVersion}");

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
        var now = DateTime.Now;
        
        // Log time until next auto-shutdown
        if (_autoShutdownAfterSeconds > 0)
        {
            var remainingSeconds = (int)(_autoShutdownTime - now).TotalSeconds;
            if (remainingSeconds > 0)
            {
                _log.Info($"Auto-shutdown in {remainingSeconds} seconds.");
            }
        }

        // Log time until next update check
        if (_checkForUpdates)
        {
            var remainingSeconds = (int)(_updateCheckTime - now).TotalSeconds;
            if (remainingSeconds > 0)
            {
                _log.Info($"Next update check in {remainingSeconds} seconds.");
            }
            else
            {
                _log.Debug($"Update check is due now.");
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
        _serviceStartedAt = DateTime.Now;
        _log.Debug($"Service started at {_serviceStartedAt:yyyy-MM-dd HH:mm:ss.fff}. Minimum uptime for first update check is {_minimumServerUptimeForUpdateSeconds} seconds.");
        
        var updateCheckIntervalSeconds = _updateCheckIntervalsSeconds.FirstOrDefault();
        _updateCheckTime = _serviceStartedAt.AddSeconds(updateCheckIntervalSeconds);
        _log.Debug($"Scheduling next update check for {_updateCheckTime:yyyy-MM-dd HH:mm:ss.fff}.");
        if (_updateCheckIntervalsSeconds.Count > 1) //keep the last item in the list. it will be used for subsequent checks.
        {
            _updateCheckIntervalsSeconds.Remove(updateCheckIntervalSeconds);
            _log.Debug($"Removing used update check interval of {updateCheckIntervalSeconds} seconds from the list.");
        }   
    }
    private void SetAutoShutdownSchedule()
    {   
        _serviceStartedAt = DateTime.Now;
        _autoShutdownTime = DateTime.Now.AddSeconds(_autoShutdownAfterSeconds);
        _log.Debug($"Auto-shutdown timer reset due to server restarting post patching. Next auto-shutdown scheduled for {_autoShutdownTime:yyyy-MM-dd HH:mm:ss.fff}");        
    }
    
}