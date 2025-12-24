using MineCraftManagementService.Services;

namespace MineCraftManagementService.Interfaces;

/// <summary>
/// Provides server status with the ability to switch behavior during shutdown.
/// </summary>
public interface IServerStatusProvider
{
    /// <summary>
    /// Gets the current server status.
    /// </summary>
    Task<MineCraftServerStatus> GetStatusAsync();
    
    /// <summary>
    /// Switches to shutdown mode, which returns ShouldBeStopped once, then ShouldBeIdle.
    /// This prevents the server from restarting when the Windows service is stopping.
    /// </summary>
    void SetShutdownMode();
}
