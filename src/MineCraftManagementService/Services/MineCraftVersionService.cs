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
    private readonly IMineCraftApiClient _apiClient;

    public MineCraftVersionService(
        ILog<MineCraftVersionService> log,
        IMineCraftApiClient apiClient)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    /// <summary>
    /// Gets the latest Bedrock server version.
    /// </summary>
    public async Task<MineCraftServerDownload?> GetLatestVersionAsync(CancellationToken cancellationToken)
    {
        return await _apiClient.GetLatestVersionAsync(cancellationToken);
    }
}
