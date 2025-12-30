namespace MineCraftManagementService.Services;

/// <summary>
/// Holds the Handlers used by ServerStatusProvider:
/// - NormalStatusHandler: Returns the actual server lifecycle status during normal operation
/// - ShutdownStatusHandler: Returns ShouldBeStopped then ShouldBeIdle during Windows service shutdown
/// - AutoShutdownTimeExceededHandler: Should always return ShouldBeIdle post auto-shutdown trigger to prevent restart
/// </summary>
public class ServerStatusHandlers
{
    /// <summary>
    /// The Handler that returns real server lifecycle status during normal operation.
    /// </summary>
    public required Func<Task<MineCraftServerLifecycleStatus>> NormalStatusHandler { get; init; }

    /// <summary>
    /// The handler that returns shutdown sequence (ShouldBeStopped then ShouldBeIdle).
    /// Prevents server restart during Windows service shutdown.
    /// </summary>
    public required Func<Task<MineCraftServerLifecycleStatus>> WindowsServiceShutdownStatusHandler { get; init; }

    /// <summary>
    /// A handler that always returns ShouldBeStopped once the server has exceeded the auto-shutdown timer, preventing any restart.
    /// </summary>
    public required Func<Task<MineCraftServerLifecycleStatus>> AutoShutdownTimeExceededHandler { get; init; }
}

