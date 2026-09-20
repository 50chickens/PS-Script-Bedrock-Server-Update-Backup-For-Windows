namespace MineCraftManagementService.Interfaces;

public interface IMineCraftHttpClient
{
    Task<string> GetStringAsync(string url, CancellationToken cancellationToken);
    Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken);
}
