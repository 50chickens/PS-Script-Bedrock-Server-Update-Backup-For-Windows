using MineCraftManagementService.Logging;
using NLog.Config;

namespace MineCraftManagementService.Tests
{
    public static class NUnitBuilderExtensions
    {
        public static LogBuilder UseNunitTestContext(this LogBuilder logBuilder, Common.Logging.LogLevel? logLevel = null)
        {
            var target = new NUnitLogTarget();
            var level = logLevel ?? logBuilder.Context.LogLevel;
            var loggingRule = new LoggingRule("*", level.ToNlogLogLevel(), target);
            logBuilder.AddRegistration(new LogRegistration
            {
                Target = target,
                LoggingRules = new List<LoggingRule> { loggingRule },
            });
            return logBuilder;
        }
    }
}
