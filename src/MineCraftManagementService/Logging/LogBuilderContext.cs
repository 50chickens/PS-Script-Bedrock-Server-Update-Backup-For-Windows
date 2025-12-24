using Common.Logging;
using Microsoft.Extensions.Configuration;

namespace MineCraftManagementService.Logging
{
    public class LogBuilderContext
    {
        private readonly IConfiguration _configuration;
        public LoggingSettings Settings { get; }
        public LogLevel LogLevel { get; internal set; }

        public LogBuilderContext(IConfiguration configuration)
        {
            _configuration = configuration;
            Settings = configuration.GetSection("Logging").Get<LoggingSettings>() ?? new LoggingSettings();
        }
    }
}
