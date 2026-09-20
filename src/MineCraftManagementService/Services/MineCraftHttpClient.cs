using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;

namespace MineCraftManagementService.Services;

public class MineCraftHttpClient : IMineCraftHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILog<MineCraftHttpClient> _log;

    public MineCraftHttpClient(HttpClient httpClient, ILog<MineCraftHttpClient> log)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.GetStringAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _log.Error(ex, $"HTTP request failed for URL: {url}");
            throw;
        }
    }

    public async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024);

            await contentStream.CopyToAsync(fileStream, 1024 * 1024, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _log.Error(ex, $"Failed to download file from {url} to {destinationPath}");
            throw;
        }
    }
}
