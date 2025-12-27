namespace MineCraftManagementService.Services;

/// <summary>
/// Provides the shutdown status sequence that returns ShouldBeStopped once, then ShouldBeIdle.
/// This prevents the server from restarting when the Windows service is stopping.
/// </summary>
public class ShutdownStatusFunc
{
    private bool _returnedStopped;

    public ShutdownStatusFunc()
    {
        _returnedStopped = false;
    }

    /// <summary>
    /// Returns ShouldBeStopped once, then returns ShouldBeIdle for all subsequent calls.
    /// </summary>
    public async Task<MineCraftServerLifecycleStatus> GetStatusAsync()
    {
        if (!_returnedStopped)
        {
            _returnedStopped = true;
            return new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeStopped };
        }
        return new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeIdle };
    }
}
