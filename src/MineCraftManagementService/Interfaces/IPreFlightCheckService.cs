namespace MineCraftManagementService.Interfaces;

public interface IPreFlightCheckService
{
    Task<bool> CheckAndCleanupAsync();
}
