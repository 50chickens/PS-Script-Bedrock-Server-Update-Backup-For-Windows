using MineCraftManagementService.Enums;
using MineCraftManagementService.Interfaces;

namespace MineCraftManagementService.Services;

/// <summary>
/// Provides server lifecycle status with the ability to switch behavior during shutdown.
/// Normally delegates to the normal status handler for real status checks.
/// During Windows service shutdown, switches to the shutdown handler which returns ShouldBeStopped then ShouldBeIdle to prevent restart.
/// During auto-shutdown, uses the auto-shutdown handler to temporarily prevent restart.
/// </summary>
public class ServerStatusProvider : IServerStatusProvider
{
    private readonly ServerStatusHandlers _statusHandler;
    private bool _windowsServiceIsShuttingDown;
    private bool _autoShutdownTimeExceeded;

    public ServerStatusProvider(ServerStatusHandlers statusHandler)
    {
        _statusHandler = statusHandler ?? throw new ArgumentNullException(nameof(statusHandler));
        _windowsServiceIsShuttingDown = false;
        _autoShutdownTimeExceeded = false;
    }

    /// <summary>
    /// Gets the appropriate status handler based on current operational mode.
    /// Priority: WindowsServiceShutdown > AutoShutdown > Normal
    /// </summary>
    public Func<Task<MineCraftServerLifecycleStatus>> GetStatusHandler =>
        _windowsServiceIsShuttingDown ? _statusHandler.WindowsServiceShutdownStatusHandler
        : _autoShutdownTimeExceeded ? _statusHandler.AutoShutdownTimeExceededHandler
        : _statusHandler.NormalStatusHandler;

    /// <summary>
    /// Gets the current server lifecycle status using the active handler.
    /// </summary>
    public async Task<MineCraftServerLifecycleStatus> GetLifeCycleStateAsync()
    {
        return await GetStatusHandler();
    }

    /// <summary>
    /// Switches to shutdown mode, which returns ShouldBeStopped once, then ShouldBeIdle.
    /// This prevents the server from restarting when the Windows service is stopping.
    /// </summary>
    public void SetShutdownMode(ServerShutDownMode mode)
    {
        switch (mode)
        {
            case ServerShutDownMode.WindowsServiceShutdown:
                _windowsServiceIsShuttingDown = true;
                break;
            case ServerShutDownMode.DenyRestart:
                _autoShutdownTimeExceeded = true;
                break;
            case ServerShutDownMode.AllowRestart:
                _windowsServiceIsShuttingDown = false;
                _autoShutdownTimeExceeded = false;
                break;
        }
    }
}
