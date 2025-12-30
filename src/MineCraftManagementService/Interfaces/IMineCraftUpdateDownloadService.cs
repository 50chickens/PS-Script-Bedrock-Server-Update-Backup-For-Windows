using MineCraftManagementService.Models;

namespace MineCraftManagementService.Interfaces;

public interface IMineCraftUpdateDownloadService
{
    Task<(bool, string)> DownloadUpdateAsync(MineCraftServerDownload mineCraftServerDownload, CancellationToken cancellationToken);
}