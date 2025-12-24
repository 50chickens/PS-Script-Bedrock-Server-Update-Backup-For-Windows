namespace MineCraftManagementService.Services;

/// <summary>
/// Holds the two Funcs used by ServerStatusProvider:
/// - NormalStatusFunc: Returns the actual server status during normal operation
/// - ShutdownStatusFunc: Returns ShouldBeStopped then ShouldBeIdle during shutdown
/// </summary>
public class ServerStatusFuncs
{
    /// <summary>
    /// The Func that returns real server status during normal operation.
    /// </summary>
    public required Func<Task<MineCraftServerStatus>> NormalStatusFunc { get; init; }

    /// <summary>
    /// The Func that returns shutdown sequence (ShouldBeStopped then ShouldBeIdle).
    /// Prevents server restart during Windows service shutdown.
    /// </summary>
    public required Func<Task<MineCraftServerStatus>> ShutdownStatusFunc { get; init; }
}
