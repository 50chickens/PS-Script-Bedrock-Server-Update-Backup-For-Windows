namespace MineCraftManagementService.Models;

public class MineCraftServerOptions
{
    public const string Settings = "MineCraftServer";
    public required string MineCraftVersionApiUrl { get; set; }
    public required string ServerPath { get; set; }
    public required string ServerExecutableName { get; set; }
    public bool UpdateMineCraftOnServiceStart { get; set; }
    public bool CheckForUpdates { get; set; }
    public required string BackupFolderName { get; set; }
    public bool BackupOnlyUserData { get; set; } = false;
    public string DownloadFolderName { get; set; } = "";
    public string DownloadFileName { get; set; } = "bedrock-server.zip";
    public bool OverwriteExistingDownloadFile { get; set; } = false;
    public int MaxMemoryMB { get; set; }
    public int StartTimeoutSeconds { get; set; }
    public int StopTimeoutSeconds { get; set; }
    public bool EnableAutoStart { get; set; }
    public int AutoStartDelaySeconds { get; set; }
    public int AutoShutdownAfterSeconds { get; set; }
    
    // Server port configuration
    public required int[] ServerPorts { get; set; }
    
    // Graceful shutdown configuration
    public int GracefulShutdownMaxWaitSeconds { get; set; }
    public int GracefulShutdownCheckIntervalMs { get; set; }
    public required string GracefulShutdownCommand { get; set; }    
    // Monitoring configuration
    public int MonitoringIntervalSeconds { get; set; }
    public int UpdateCheckIntervalSeconds { get; set; }
    public int MinimumServerUptimeForUpdateSeconds { get; set; }
    public int DownloadTimeoutSeconds { get; set; } = 300;
}
