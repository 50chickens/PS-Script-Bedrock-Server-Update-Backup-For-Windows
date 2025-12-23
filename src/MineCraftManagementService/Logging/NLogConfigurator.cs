using NLog;
using NLog.Config;
using NLog.Targets;

namespace MineCraftManagementService.Logging;

public static class NLogConfigurator
{
    public static void Configure()
    {
        var config = new LoggingConfiguration();

        // File target
        var fileTarget = new FileTarget("allfile")
        {
            FileName = "logs/MineCraftManagementService-${shortdate}.log",
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message} ${onexception:${newline}${exception:format=tostring}}",
            ConcurrentWrites = true,
            KeepFileOpen = false
        };

        // Console target
        var consoleTarget = new ConsoleTarget("console")
        {
            Layout = @"${date:format=HH\:mm\:ss}|${level:uppercase=true}|${logger:shortName=true}|${message}"
        };

        config.AddTarget(fileTarget);
        config.AddTarget(consoleTarget);

        // Rules
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget);
        config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);

        LogManager.Configuration = config;
    }
}