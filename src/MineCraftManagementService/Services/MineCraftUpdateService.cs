using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles checking for Bedrock server updates, downloading them
/// </summary>
public class MineCraftUpdateService : IMineCraftUpdateService
{
    private IMineCraftVersionService _mineCraftVersionService;
    private readonly ILog<MineCraftUpdateService> _log;
    private readonly IMineCraftServerService _minecraftService;
    private MineCraftServerOptions _options;
    private readonly int _minimumServerUptimeForUpdateSeconds;

    public MineCraftUpdateService(
        ILog<MineCraftUpdateService> log,
        IMineCraftServerService minecraftService,
        IMineCraftVersionService versionService,
        MineCraftServerOptions options)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _mineCraftVersionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _minimumServerUptimeForUpdateSeconds = _options.MinimumServerUptimeForUpdateSeconds;
    }

    /// <summary>
    /// Checks for updates once per day by comparing the latest version from Microsoft API
    /// with the provided current version.
    /// </summary>
    public async Task<(bool, string, string)> NewVersionIsAvailable(string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check server uptime - don't check for updates if server hasn't been up long enough
            var serverUptime = _minecraftService.ServerStartTime == DateTime.MinValue
                ? TimeSpan.Zero
                : DateTime.Now - _minecraftService.ServerStartTime;

            _log.Debug($"NewVersionIsAvailable called. Current version: {currentVersion}, Server uptime: {serverUptime.TotalSeconds:F0}s");

            if (serverUptime.TotalSeconds < _minimumServerUptimeForUpdateSeconds)
            {
                _log.Debug($"Update check skipped. Server uptime is {serverUptime.TotalSeconds:F0} seconds. Have not yet reached minimum uptime of {_minimumServerUptimeForUpdateSeconds} seconds.");
                return (false, $"Update check skipped: server uptime {serverUptime.TotalSeconds:F0}s < minimum {_minimumServerUptimeForUpdateSeconds}s", "");
            }

            _log.Info("Checking for Bedrock server updates...");

            var minecraftServerVersion = await _mineCraftVersionService.GetLatestVersionAsync(cancellationToken);
            if (minecraftServerVersion == null)
            {
                _log.Warn("Failed to determine latest Bedrock server version");
                return (false, "Failed to determine latest Bedrock server version", "");
            }

            _log.Info($"Current version: {currentVersion}, Latest version: {minecraftServerVersion.Version}");

            // Compare versions
            if (minecraftServerVersion.Version == currentVersion)
            {
                _log.Info("Bedrock server is up to date");
                return (false, "Bedrock server is up to date", "");
            }

            _log.Info($"Update available: {currentVersion} → {minecraftServerVersion.Version}");
            return (true, $"Update available: {currentVersion} → {minecraftServerVersion.Version}", minecraftServerVersion.Version);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception in NewVersionIsAvailable");
            return (false, $"Exception checking for updates: {ex.Message}", "");
        }
    }

    /// <summary>
    /// Copies a directory recursively, excluding certain folders.
    /// </summary>
    private void CopyDirectory(string source, string destination, string[] excludeDirs)
    {
        var sourceDir = new DirectoryInfo(source);
        if (!sourceDir.Exists)
            return;

        if (!Directory.Exists(destination))
            Directory.CreateDirectory(destination);

        foreach (var file in sourceDir.GetFiles())
        {
            file.CopyTo(Path.Combine(destination, file.Name), true);
        }

        foreach (var dir in sourceDir.GetDirectories())
        {
            if (excludeDirs.Contains(dir.Name))
                continue;

            var nextDestination = Path.Combine(destination, dir.Name);
            CopyDirectory(dir.FullName, nextDestination, excludeDirs);
        }
    }

    // Helper methods to extract version from different API responses

}

