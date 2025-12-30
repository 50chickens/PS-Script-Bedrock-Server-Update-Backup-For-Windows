namespace MineCraftManagementService.Services;

/// <summary>
/// Provides auto-shutdown status handler that always returns ShouldBeStopped to prevent restart after auto-shutdown timer is exceeded.
/// </summary>
public class AutoShutdownTimeExceededStatusHandler
{
    /// <summary>
    /// Always returns ShouldBeIdle to keep the server idle once auto-shutdown timer is exceeded.
    /// Prevents repeated stop attempts after server is already stopped.
    /// </summary>
    public Task<MineCraftServerLifecycleStatus> GetStatusAsync()
    {
        return Task.FromResult(
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeIdle }
        );
    }
}