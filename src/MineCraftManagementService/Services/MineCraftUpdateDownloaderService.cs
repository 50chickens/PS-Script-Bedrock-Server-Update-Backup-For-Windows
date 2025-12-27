using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles checking for Bedrock server updates, downloading them
/// </summary>
public class MineCraftUpdateDownloaderService : IMineCraftUpdateDownloaderService
{
    private readonly ILog<MineCraftUpdateDownloaderService> _log;
    
    private readonly string _serverPath;
    private MineCraftServerOptions _options;
    private DateTime _lastUpdateCheckTime = DateTime.MinValue;
    private readonly TimeSpan _updateCheckInterval = TimeSpan.FromHours(24);

    public MineCraftUpdateDownloaderService(
        ILog<MineCraftUpdateDownloaderService> logger,
        MineCraftServerOptions options)
    {
        _log = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serverPath = _options.ServerPath;
    }
    public async Task<string> DownloadUpdateAsync(string url, CancellationToken cancellationToken)
    {
        
            _log.Info($"Downloading Bedrock server version from {url}...");
            
            // Download the update
            var downloadFileName = Path.Combine(Path.GetTempPath(), _options.DownloadFileName);
            using (var httpClient = new HttpClient())
            {
                // Use configurable timeout for download
                httpClient.Timeout = TimeSpan.FromSeconds(_options.DownloadTimeoutSeconds);
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = File.Create(downloadFileName);
                // Use larger buffer for faster copying
                await contentStream.CopyToAsync(fileStream, 1024 * 1024, cancellationToken);  // 1MB buffer
            }

            _log.Info($"Update downloaded to {downloadFileName}");
           return downloadFileName;
    }
}



