using MineCraftManagementService.Models;

namespace MineCraftManagementService.Interfaces;

/// <summary>
/// Client for communicating with the Minecraft API to retrieve version information and download links.
/// </summary>
public interface IMineCraftApiClient
{
    /// <summary>
    /// Gets the latest Minecraft Bedrock server version information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Server download information or null if unable to retrieve.</returns>
    Task<MineCraftServerDownload?> GetLatestVersionAsync(CancellationToken cancellationToken = default);
}
