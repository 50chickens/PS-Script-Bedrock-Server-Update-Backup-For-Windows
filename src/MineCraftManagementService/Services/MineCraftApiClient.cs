using MineCraftManagementService.Extensions;
using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// HTTP client for communicating with the Minecraft API.
/// Uses IHttpClientFactory for proper HttpClient lifecycle management.
/// </summary>
public class MineCraftApiClient : IMineCraftApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILog<MineCraftApiClient> _log;
    private readonly string _apiUrl;

    public MineCraftApiClient(HttpClient httpClient, ILog<MineCraftApiClient> log, MineCraftServerOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _apiUrl = options?.MineCraftVersionApiUrl ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the latest Bedrock server version.
    /// </summary>
    public async Task<MineCraftServerDownload?> GetLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(_apiUrl, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _log.Error($"Microsoft API returned non-success status code {response.StatusCode}. URL: {_apiUrl}. Content: {content}");
                return null;
            }

            if (content.TryGetMineCraftServer(out var mineCraftServer))
            {
                _log.Info($"Latest Minecraft Bedrock version: {mineCraftServer.Version}");
                return mineCraftServer;
            }
            
            _log.Error($"Failed to parse Minecraft server version from API response. URL: {_apiUrl}. Content length: {content.Length} bytes");
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _log.Warn("Version check was cancelled");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _log.Error(ex, $"HTTP request failed when fetching latest version from {_apiUrl}");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _log.Error(ex, $"Request timed out when fetching latest version from {_apiUrl}");
            return null;
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Unexpected error in GetLatestVersionAsync. URL: {_apiUrl}");
            return null;
        }
    }
}
