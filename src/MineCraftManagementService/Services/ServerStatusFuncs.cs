namespace MineCraftManagementService.Services;

/// <summary>
/// Holds the Funcs used by ServerStatusProvider:
/// - NormalStatusFunc: Returns the actual server lifecycle status during normal operation
/// - ShutdownStatusFunc: Returns ShouldBeStopped then ShouldBeIdle during Windows service shutdown
/// - AutoShutdownIdleFunc: Returns ShouldBeIdle during auto-shutdown cooldown period
/// </summary>
public class ServerStatusFuncs
{
    /// <summary>
    /// The Func that returns real server lifecycle status during normal operation.
    /// </summary>
    public required Func<Task<MineCraftServerLifecycleStatus>> NormalStatusFunc { get; init; }

    /// <summary>
    /// The Func that returns shutdown sequence (ShouldBeStopped then ShouldBeIdle).
    /// Prevents server restart during Windows service shutdown.
    /// </summary>
    public required Func<Task<MineCraftServerLifecycleStatus>> ShutdownStatusFunc { get; init; }

    /// <summary>
    /// The Func that returns ShouldBeIdle during auto-shutdown cooldown.
    /// Used when auto-shutdown timer is exceeded and server is stopped to prevent immediate restart.
    /// </summary>
    public required Func<Task<MineCraftServerLifecycleStatus>> AutoShutdownIdleFunc { get; init; }
}

