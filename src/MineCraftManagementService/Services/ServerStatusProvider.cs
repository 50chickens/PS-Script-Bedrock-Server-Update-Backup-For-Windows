using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Services;

namespace MineCraftManagementService.Services;

/// <summary>
/// Provides server status with the ability to switch behavior during shutdown.
/// Normally delegates to the normal status Func for real status checks.
/// During shutdown, switches to the shutdown Func which returns ShouldBeStopped then ShouldBeIdle to prevent restart.
/// </summary>
public class ServerStatusProvider : IServerStatusProvider
{
    private readonly ServerStatusFuncs _statusFuncs;
    private bool _isShuttingDown;

    public ServerStatusProvider(ServerStatusFuncs statusFuncs)
    {
        _statusFuncs = statusFuncs ?? throw new ArgumentNullException(nameof(statusFuncs));
        _isShuttingDown = false;
    }

    /// <summary>
    /// Gets the appropriate status Func based on whether we're shutting down.
    /// </summary>
    public Func<Task<MineCraftServerStatus>> GetStatusFunc => 
        _isShuttingDown ? _statusFuncs.ShutdownStatusFunc : _statusFuncs.NormalStatusFunc;

    /// <summary>
    /// Gets the current server status using the active Func.
    /// </summary>
    public async Task<MineCraftServerStatus> GetStatusAsync()
    {
        return await GetStatusFunc();
    }

    /// <summary>
    /// Switches to shutdown mode, which returns ShouldBeStopped once, then ShouldBeIdle.
    /// This prevents the server from restarting when the Windows service is stopping.
    /// </summary>
    public void SetShutdownMode()
    {
        _isShuttingDown = true;
    }
}
