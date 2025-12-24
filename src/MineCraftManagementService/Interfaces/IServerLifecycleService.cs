namespace MineCraftManagementService.Interfaces;

public interface IServerLifecycleService
{
    Task ManageServerLifecycleAsync(CancellationToken cancellationToken = default);
    Task StopServerAsync();
}
