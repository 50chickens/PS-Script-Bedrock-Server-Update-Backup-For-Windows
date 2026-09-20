using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles checking for Bedrock server updates, downloading them
/// </summary>
public class MineCraftUpdateDownloadService : IMineCraftUpdateDownloadService
{
    private readonly ILog<MineCraftUpdateDownloadService> _log;
    private readonly IMineCraftHttpClient _httpClient;
    private MineCraftServerOptions _options;

    public MineCraftUpdateDownloadService(
        ILog<MineCraftUpdateDownloadService> log,
        IMineCraftHttpClient httpClient,
        MineCraftServerOptions options)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<(bool, string)> DownloadUpdateAsync(MineCraftServerDownload mineCraftServerDownload, CancellationToken cancellationToken)
    {

        _log.Info($"Downloading Bedrock server version from {mineCraftServerDownload.Url}...");

        // Download the update
        var downloadFileName = Path.Combine(Path.GetTempPath(), _options.DownloadFileName);
        await _httpClient.DownloadFileAsync(mineCraftServerDownload.Url, downloadFileName, cancellationToken);

        _log.Info($"Update downloaded to {downloadFileName}");
        return (true, downloadFileName);
    }

}