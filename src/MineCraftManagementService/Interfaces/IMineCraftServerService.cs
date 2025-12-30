namespace MineCraftManagementService.Interfaces;

public interface IMineCraftServerService
{
    bool IsRunning { get; }
    DateTime ServerStartTime { get; }
    string CurrentVersion { get; }
    Task<bool> StartServerAsync();
    Task<bool> TryGracefulShutdownAsync();
    Task<bool> ForceStopServerAsync();
}
