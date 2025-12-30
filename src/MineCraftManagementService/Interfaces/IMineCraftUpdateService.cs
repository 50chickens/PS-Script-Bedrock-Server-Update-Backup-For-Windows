namespace MineCraftManagementService.Interfaces;

public interface IMineCraftUpdateService
{
    /// <summary>
    /// Checks if a new version is available by comparing the latest version from Microsoft API
    /// with the provided current version.
    /// </summary>
    /// <param name="currentVersion">The current server version to compare against</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (updateAvailable, message, newVersion)</returns>
    Task<(bool, string, string)> NewVersionIsAvailable(string currentVersion, CancellationToken cancellationToken = default);
}

