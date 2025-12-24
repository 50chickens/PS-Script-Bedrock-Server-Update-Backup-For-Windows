namespace MineCraftManagementService.Interfaces;

public interface IMineCraftUpdateDownloaderService
{
    Task<string> DownloadUpdateAsync(string url, CancellationToken cancellationToken);
}
