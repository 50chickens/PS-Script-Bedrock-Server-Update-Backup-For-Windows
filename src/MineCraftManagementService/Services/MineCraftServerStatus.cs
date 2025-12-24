namespace MineCraftManagementService.Services;

public enum MineCraftServerStatus
{
    ShouldBeStarted,
    ShouldBePatched,
    ShouldBeStopped,
    ShouldBeMonitored,
    ShouldBeIdle,
    Starting,
    Stopping,
    Error
}