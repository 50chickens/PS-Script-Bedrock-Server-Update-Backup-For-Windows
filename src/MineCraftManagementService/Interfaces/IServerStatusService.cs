using MineCraftManagementService.Services;

namespace MineCraftManagementService.Interfaces;

public interface IServerStatusService
{
    Task<MineCraftServerStatus> GetStatusAsync();
}
