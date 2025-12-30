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

/// <summary>
/// Represents the current lifecycle status of the Minecraft server along with any patch version info.
/// When LifecycleStatus is ShouldBePatched, PatchVersion contains the version to be installed.
/// </summary>
public class MineCraftServerLifecycleStatus
{
    /// <summary>
    /// The current lifecycle status of the server.
    /// </summary>
    public MineCraftServerStatus LifecycleStatus { get; set; }

    /// <summary>
    /// The version to be patched/installed when LifecycleStatus is ShouldBePatched.
    /// Null for all other statuses.
    /// </summary>
    public string? PatchVersion { get; set; }
}