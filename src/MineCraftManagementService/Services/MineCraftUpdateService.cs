using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles checking for Bedrock server updates, downloading them
/// </summary>
public class MineCraftUpdateService : IMineCraftUpdateService
{
    private readonly ILog<MineCraftUpdateService> _log;
    private readonly IMineCraftServerService _minecraftService;
    
    private readonly string _serverPath;
    private MineCraftServerOptions _options;
    private readonly int _minimumServerUptimeForUpdateSeconds;

    public MineCraftUpdateService(
        ILog<MineCraftUpdateService> logger,
        IMineCraftServerService minecraftService,
        MineCraftServerOptions options)
    {
        _log = logger ?? throw new ArgumentNullException(nameof(logger));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serverPath = _options.ServerPath;
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

            var latestVersion = await GetLatestVersionAsync(cancellationToken);
            if (latestVersion == null)
            {
                _log.Warn("Failed to determine latest Bedrock server version");
                return (false, "Failed to determine latest Bedrock server version", "");
            }

            _log.Info($"Current version: {currentVersion}, Latest version: {latestVersion}");

            // Compare versions
            if (latestVersion == currentVersion)
            {
                _log.Info("Bedrock server is up to date");
                return (false, "Bedrock server is up to date", "");
            }

            _log.Info($"Update available: {currentVersion} → {latestVersion}");
            return (true, $"Update available: {currentVersion} → {latestVersion}", latestVersion);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception in NewVersionIsAvailable");
            return (false, $"Exception checking for updates: {ex.Message}", "");
        }
    }

    /// <summary>
    /// Gets the latest Bedrock server version.
    /// </summary>
    private async Task<string> GetLatestVersionAsync(CancellationToken cancellationToken)
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
            
            var version = ExtractVersionFromMicrosoftApi(content);
            _log.Info("Latest version from Microsoft API: " + version);
            return version;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception in GetLatestVersionAsync");
            return null;
        }
    }

    /// <summary>
    /// Downloads and applies the update by backing up and replacing files.
    /// </summary>
    public async Task<(bool, string)> DownloadLatestUpdateAsync(string version, CancellationToken cancellationToken)
    {
        
            _log.Info($"Downloading Bedrock server version {version}...");
            // Get download URL for the version
            var downloadUrl = await GetDownloadUrlForVersionAsync(version, cancellationToken);
            
            _log.Info($"Download URL: {downloadUrl}");

            // Download the update
            var tempFile = Path.Combine(Path.GetTempPath(), $"bedrock-server-{version}.zip");
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromMinutes(10);
                using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = File.Create(tempFile);
                await contentStream.CopyToAsync(fileStream, cancellationToken);
            }

            _log.Info($"Update downloaded to {tempFile}");
           return (true, tempFile);
    }

    /// <summary>
    /// Gets the download URL for a specific Bedrock version.
    /// </summary>
    private async Task<string> GetDownloadUrlForVersionAsync(string version, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

            var response = await httpClient.GetAsync(
                "https://net-secondary.web.minecraft-services.net/api/v1.0/download/links",
                cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var url = ExtractDownloadUrlFromMicrosoftApi(content);
                
        // Construct URL from version
        return $"https://www.minecraft.net/bedrockdedicatedserver/bin-win/bedrock-server-{version}.zip";
    }

    /// <summary>
    /// Creates a backup of the current server installation.
    /// </summary>
    private string CreateBackup()
    {
        var backupDir = _options.BackupFolderName;
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"bedrock-server-backup-{timestamp}");
        Directory.CreateDirectory(backupPath);

        // Copy all files except certain exclusions
        CopyDirectory(_serverPath, backupPath, new[] { "logs", "ScriptBackups", "backups" });

        return backupPath;
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

    /// <summary>
    /// Extracts the update ZIP file to the server path.
    /// </summary>
    private void ExtractUpdateToServerPath(string zipPath)
    {
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, _serverPath, overwriteFiles: true);
    }

    // Helper methods to extract version from different API responses
    private string ExtractVersionFromMicrosoftApi(string jsonContent)
    {
        var match = System.Text.RegularExpressions.Regex.Match(jsonContent, @"bedrock-server-([\d\.]+)\.zip");
        return match.Groups[1].Value;
    }

    private string ExtractDownloadUrlFromMicrosoftApi(string jsonContent)
    {
        var match = System.Text.RegularExpressions.Regex.Match(jsonContent, @"""downloadUrl""\s*:\s*""([^""]+)""");
        return match.Groups[1].Value;
    }
}



