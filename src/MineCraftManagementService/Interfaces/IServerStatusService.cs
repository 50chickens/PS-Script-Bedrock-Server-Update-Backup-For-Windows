using MineCraftManagementService.Services;

namespace MineCraftManagementService.Interfaces;

public interface IServerStatusService
{
    Task<MineCraftServerLifecycleStatus> GetLifeCycleStateAsync();
    void RescheduleNextUpdateCheck(int updateCheckIntervalSeconds);
}
