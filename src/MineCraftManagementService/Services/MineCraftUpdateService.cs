using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles checking for Bedrock server updates, downloading them
/// </summary>
public class MineCraftUpdateService
{
    private readonly ILog<MineCraftUpdateService> _log;
    
    private readonly string _serverPath;
    private MineCraftServerOptions _options;
    private DateTime _lastUpdateCheckTime = DateTime.MinValue;
    private readonly TimeSpan _updateCheckInterval = TimeSpan.FromHours(24);

    public MineCraftUpdateService(
        ILog<MineCraftUpdateService> logger,
        MineCraftServerService minecraftService,
        MineCraftServerOptions options)
    {
        _log = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serverPath = _options.ServerPath;
    }

    /// <summary>
    /// Checks for updates once per day and applies them if available.
    /// </summary>
    public async Task<(bool, string, string)> NewVersionIsAvailable(CancellationToken cancellationToken = default)
    {
        // Check if 24 hours have passed since last update check
        if (DateTime.UtcNow - _lastUpdateCheckTime < _updateCheckInterval)
        {
            _log.Debug($"Update check skipped (last check was {DateTime.UtcNow - _lastUpdateCheckTime:hh\\:mm\\:ss} ago)");
            return (false, "Update check skipped: checked recently.", "");
        }

        _lastUpdateCheckTime = DateTime.UtcNow;

        _log.Info("Checking for Bedrock server updates...");

        var latestVersion = await GetLatestVersionAsync(cancellationToken);
        if (latestVersion == null)
        {
            _log.Warn("Failed to determine latest Bedrock server version");
            return (false, "Failed to determine latest Bedrock server version", "");
        }

        var currentVersion = GetCurrentVersion();
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

    /// <summary>
    /// Gets the latest Bedrock server version.
    /// </summary>
    private async Task<string> GetLatestVersionAsync(CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        _log.Debug("Fetching latest version from Microsoft API...");
        var response = await httpClient.GetAsync(
            _options.MineCraftVersionApiUrl,
            cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var version = ExtractVersionFromMicrosoftApi(content);
            _log.Info("Latest version from Microsoft API: " + version);
            return version;

    }

    /// <summary>
    /// Gets the current Bedrock server version from server.properties.
    /// </summary>
    private string GetCurrentVersion()
    {
        var propertiesFile = Path.Combine(_serverPath, "server.properties");
        if (!File.Exists(propertiesFile))
        {
            _log.Warn("server.properties not found");
            return "unknown";
        }

        // Try to get version from the executable name or a version file
        var versionFile = Path.Combine(_serverPath, "VERSION.TXT");
        if (File.Exists(versionFile))
        {
            var version = File.ReadAllText(versionFile).Trim();
            if (!string.IsNullOrEmpty(version))
                return version;
        }

        // Fallback: return unknown
        return "unknown";
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



