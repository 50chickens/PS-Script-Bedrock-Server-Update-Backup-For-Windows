namespace MineCraftManagementService.Services;

/// <summary>
/// Provides auto-shutdown status handler that always returns ShouldBeStopped to prevent restart after auto-shutdown timer is exceeded.
/// </summary>
public class AutoShutdownTimeExceededStatusHandler
{
    /// <summary>
    /// Always returns ShouldBeStopped to prevent the server from restarting once auto-shutdown timer is exceeded.
    /// </summary>
    public Task<MineCraftServerLifecycleStatus> GetStatusAsync()
    {
        return Task.FromResult(
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeStopped }
        );
    }

    /// <summary>
    /// Reset method for compatibility. Has no effect since handler always returns ShouldBeStopped.
    /// </summary>
    public void Reset()
    {
        // No state to reset - always returns ShouldBeStopped
    }
}