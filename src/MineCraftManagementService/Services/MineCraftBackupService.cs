using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles checking for Bedrock server updates, downloading them
/// </summary>
public class MineCraftBackupService
{
    private readonly ILog<MineCraftBackupService> _log;
    
    private readonly string _serverPath;
    private MineCraftServerOptions _options;
    public MineCraftBackupService(
        ILog<MineCraftBackupService> logger,
        MineCraftServerOptions options)
    {
        _log = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serverPath = _options.ServerPath;
    }

    /// <summary>
    /// Creates a zip file backup of the current server installation.
    /// </summary>
    public string CreateBackupZipFromServerFolder()
    {
        
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupDir = _options.BackupFolderName;
        if (!Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        var backupPath = Path.Combine(backupDir, $"minecraft_backup_{timestamp}.zip");
        _log.Info($"Creating backup of server at {backupPath}...");

        // Create ZIP archive excluding 'worlds' and 'logs' directories
        var tempBackupDir = Path.Combine(backupDir, $"temp_backup_{timestamp}");
        CopyDirectory(_serverPath, tempBackupDir, new[] { "worlds", "logs", "backups" });

        System.IO.Compression.ZipFile.CreateFromDirectory(tempBackupDir, backupPath);
        Directory.Delete(tempBackupDir, true);

        _log.Info("Backup created successfully");
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
}



