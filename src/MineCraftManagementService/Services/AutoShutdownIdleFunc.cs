namespace MineCraftManagementService.Services;

/// <summary>
/// Provides the auto-shutdown idle status that always returns ShouldBeIdle.
/// Used when the auto-shutdown timer is exceeded and the server is stopped,
/// preventing immediate restart during the shutdown cooldown period.
/// </summary>
public class AutoShutdownIdleFunc
{
    /// <summary>
    /// Always returns ShouldBeIdle to keep server idle during auto-shutdown cooldown.
    /// </summary>
    public Task<MineCraftServerLifecycleStatus> GetStatusAsync()
    {
        return Task.FromResult(
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeIdle }
        );
    }
}
