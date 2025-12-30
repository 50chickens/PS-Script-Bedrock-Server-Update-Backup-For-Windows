using MineCraftManagementService.Extensions;
using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles the auto-start logic for the Minecraft server.
/// </summary>
public class MineCraftVersionService : IMineCraftVersionService
{
    private readonly ILog<MineCraftVersionService> _log;
    private readonly MineCraftServerOptions _options;

    public MineCraftVersionService(
        ILog<MineCraftVersionService> _log,
        MineCraftServerOptions options)
    {
        this._log = _log ?? throw new ArgumentNullException(nameof(_log));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
    /// <summary>
    /// Gets the latest Bedrock server version.
    /// </summary>
    public async Task<MineCraftServerDownload> GetLatestVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            _log.Debug("Fetching latest version from Microsoft API...");
            _log.Debug($"API URL: {_options.MineCraftVersionApiUrl}");

            var response = await httpClient.GetAsync(
                _options.MineCraftVersionApiUrl,
                cancellationToken);

            _log.Debug($"HTTP Response Status: {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _log.Debug($"Response content length: {content.Length} bytes");

            if (!response.IsSuccessStatusCode)
            {
                _log.Error($"API returned non-success status: {response.StatusCode}. Content: {content}");
                return null;
            }

            if (content.TryGetMineCraftServer(out var mineCraftServer))
            {
                _log.Info("Latest version from Microsoft API: " + mineCraftServer.Version);
                return mineCraftServer;
            }
            _log.Error("Failed to extract version from Microsoft API response");
            return null;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception in GetLatestVersionAsync");
            return null;
        }
    }

}
