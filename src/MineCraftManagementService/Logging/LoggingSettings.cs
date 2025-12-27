using NLog;

namespace MineCraftManagementService.Logging
{
    public class LoggingSettings()
    {
        public bool EnableConsoleLogging { get; set; } = true;
        public bool EnableFileLogging { get; set; } = false;
        public string LogFilePath { get; set; } = "alsa_net.log";
        private string _minimumLogLevel = "Info";
        public string MinimumLogLevel 
        { 
            get => _minimumLogLevel;
            set => _minimumLogLevel = value;
        }
        public LogLevel GetLogLevel() => LogLevel.FromString(_minimumLogLevel);
    }
}
