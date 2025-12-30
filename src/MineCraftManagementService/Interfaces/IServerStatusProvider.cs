using MineCraftManagementService.Enums;
using MineCraftManagementService.Services;

namespace MineCraftManagementService.Interfaces;

/// <summary>
/// Provides server lifecycle status with the ability to switch behavior during shutdown.
/// </summary>
public interface IServerStatusProvider
{
    /// <summary>
    /// Gets the current server lifecycle state.
    /// </summary>
    Task<MineCraftServerLifecycleStatus> GetLifeCycleStateAsync();

    /// <summary>
    /// the shutdown cases are slight different during windows service shutdown vs auto-shutdown time exceeded.
    /// </summary>
    void SetShutdownMode(ServerShutDownMode shutDownMode);
}