using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles checking for Bedrock server updates, downloading them
/// </summary>
public class MineCraftBackupService : IMineCraftBackupService
{
    private readonly ILog<MineCraftBackupService> _log;

    private readonly string _serverPath;
    private MineCraftServerOptions _options;
    public MineCraftBackupService(
        ILog<MineCraftBackupService> log,
        MineCraftServerOptions options)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serverPath = _options.ServerPath;
    }

    /// <summary>
    /// Creates a zip file backup of the current server installation.
    /// If BackupOnlyUserData is true, backs up only worlds, logs, and backups folders.
    /// If BackupOnlyUserData is false, backs up the entire server folder.
    /// </summary>
    public string CreateBackupZipFromServerFolder()
    {
        // Use UTC time for consistent timestamps across time zones
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupDir = _options.BackupFolderName;
        
        try
        {
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            var backupPath = Path.Combine(backupDir, $"minecraft_backup_{timestamp}_UTC.zip");
            _log.Info($"Creating backup at {backupPath}");

            if (_options.BackupOnlyUserData)
            {
                // Backup only user data (selective folders)
                var tempBackupDir = Path.Combine(backupDir, $"temp_backup_{timestamp}");
                
                try
                {
                    CopyDirectory(_serverPath, tempBackupDir, new[] { "worlds", "logs", "backups" });
                    System.IO.Compression.ZipFile.CreateFromDirectory(tempBackupDir, backupPath);
                }
                finally
                {
                    // Cleanup temp directory even if backup fails
                    if (Directory.Exists(tempBackupDir))
                    {
                        try
                        {
                            Directory.Delete(tempBackupDir, true);
                        }
                        catch (Exception ex)
                        {
                            _log.Warn($"Failed to clean up temporary backup directory: {tempBackupDir}. Error: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                // Backup entire server folder
                System.IO.Compression.ZipFile.CreateFromDirectory(_serverPath, backupPath);
            }

            _log.Info($"Backup created successfully: {Path.GetFileName(backupPath)}");
            return backupPath;
        }
        catch (IOException ex)
        {
            _log.Error(ex, $"I/O error creating backup. Server path: {_serverPath}, Backup dir: {backupDir}");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.Error(ex, $"Access denied creating backup. Server path: {_serverPath}, Backup dir: {backupDir}");
            throw;
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

    /// <summary>
    /// Extracts the update ZIP file to the server path.
    /// </summary>
    private void ExtractUpdateToServerPath(string zipPath)
    {
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, _serverPath, overwriteFiles: true);
    }
}



