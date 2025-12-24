using Microsoft.Extensions.Configuration;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Tests
{
    public static class TestUtils
    {
        public static IConfiguration BuildTestConfiguration()
        {
            {
                var values = new Dictionary<string, string?>
            {
                {"Logging:LogLevel", "Debug"},
                {"Logging:EnableConsoleLogging", "true"},
                {"Logging:EnableFileLogging", "false"},
            };
                var configurationBuilder = new ConfigurationBuilder().AddInMemoryCollection(values);
                return configurationBuilder.Build();
            }

        }

        public static MineCraftServerOptions CreateOptions()
        {
            return new MineCraftServerOptions
            {
                ServerPath = "d:\\games\\bedrock-server",
                ServerExecutableName = "bedrock_server.exe",
                MineCraftVersionApiUrl = "https://net-secondary.web.minecraft-services.net/api/v1.0/download/links",
                BackupFolderName = "D:\\games\\backups",
                DownloadFileName = "bedrock-server.zip",
                MaxMemoryMB = 2048,
                StartTimeoutSeconds = 60,
                StopTimeoutSeconds = 30,
                EnableAutoStart = true,
                AutoShutdownAfterSeconds = 0,
                AutoStartDelaySeconds = 0,
                UpdateMineCraftOnServiceStart = false,
                CheckForUpdates = true,
                ServerPorts = [19132, 19133],
                GracefulShutdownMaxWaitSeconds = 30,
                GracefulShutdownCheckIntervalMs = 500,
                GracefulShutdownCommand = "stop",
                MonitoringIntervalSeconds = 1,
                UpdateCheckIntervalSeconds = 3600
            };
        }
    }
}
