using MineCraftManagementService.Models;

namespace MineCraftManagementService.Interfaces;

public interface IMineCraftVersionService
{
    Task<MineCraftServerDownload> GetLatestVersionAsync(CancellationToken cancellationToken);

}
