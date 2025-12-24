using NLog;

namespace MineCraftManagementService.Logging
{
    public class LoggingSettings()
    {
        public bool EnableConsoleLogging { get; set; } = true;
        public bool EnableFileLogging { get; set; } = false;
        public string LogFilePath { get; set; } = "alsa_net.log";
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Info;
    }
}
