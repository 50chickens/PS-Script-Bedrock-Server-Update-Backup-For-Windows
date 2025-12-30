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
    private MineCraftServerOptions _options;
    
    public MineCraftUpdateDownloadService(ILog<MineCraftUpdateDownloadService> log,MineCraftServerOptions options)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<(bool, string)> DownloadUpdateAsync(MineCraftServerDownload mineCraftServerDownload, CancellationToken cancellationToken)
    {
        
            _log.Info($"Downloading Bedrock server version from {mineCraftServerDownload.Url}...");
            
            // Download the update
            var downloadFileName = Path.Combine(Path.GetTempPath(), _options.DownloadFileName);
            using (var httpClient = new HttpClient())
            {
                // Use configurable timeout for download
                httpClient.Timeout = TimeSpan.FromSeconds(_options.DownloadTimeoutSeconds);
                using var response = await httpClient.GetAsync(mineCraftServerDownload.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = File.Create(downloadFileName);
                // Use larger buffer for faster copying
                await contentStream.CopyToAsync(fileStream, 1024 * 1024, cancellationToken);  // 1MB buffer
            }

            _log.Info($"Update downloaded to {downloadFileName}");
           return (true, downloadFileName);
    }

}