using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using MineCraftManagementService.Validators;

namespace MineCraftManagementService
{
    /// <summary>
    /// Encapsulates all dependency injection container configuration and service registration.
    /// This class centralizes DI setup so it can be reused across the application and tests.
    /// </summary>
    public static class ContainerBuilder
    {
        /// <summary>
        /// Builds and returns the configured host with all registered services.
        /// </summary>
        /// <param name="args">Optional command-line arguments (defaults to empty array if not provided)</param>
        /// <returns>A configured IHost with all services registered</returns>
        public static IHost Build(string[]? args = null)
        {
            args ??= [];

            var builder = Host.CreateApplicationBuilder(args);
            ConfigureServices(builder);
            return builder.Build();
        }

        /// <summary>
        /// Configures all services and logging in the provided HostApplicationBuilder.
        /// This method is used by both the main application and tests.
        /// </summary>
        /// <param name="builder">The HostApplicationBuilder to configure</param>
        public static void ConfigureServices(HostApplicationBuilder builder)
        {
            // Load logging settings from configuration
            var loggingSettings = builder.Configuration.GetSection("Logging:MineCraft").Get<LoggingSettings>() ?? new LoggingSettings();

            // Get the log level for MineCraft from the standard Logging:LogLevel configuration
            var logLevelConfig = builder.Configuration.GetSection("Logging:LogLevel").Get<Dictionary<string, string>>();
            if (logLevelConfig != null && logLevelConfig.TryGetValue("MineCraftManagementService", out var logLevel))
            {
                loggingSettings.MinimumLogLevel = logLevel;
            }

            // Configure NLog logging with settings
            builder.Logging.AddNLogConfiguration(loggingSettings);
            builder.Logging.AddNlogFactoryAdaptor();

            // Configure the app to work as a Windows Service
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "MineCraft Management Service";
            });

            // Register EventLog provider options
            LoggerProviderOptions.RegisterProviderOptions<
                EventLogSettings, EventLogLoggerProvider>(builder.Services);

            // Register custom ILog<T> factory for dependency injection with logging level support
            builder.Services.AddSingleton(typeof(ILog<>), typeof(NLogLoggerCore<>));
            // Also register ILogger<T> so it can be injected into NLogLoggerCore for level checking
            builder.Services.AddLogging();

            // Add configuration for environment
            builder.Configuration.AddEnvironmentVariables(prefix: "MINECRAFT_");

            // Add app settings for Minecraft service configuration
            builder.Services.AddOptions<MineCraftServerOptions>()
                .Bind(builder.Configuration.GetSection(MineCraftServerOptions.Settings))
                .ValidateOnStart();
            builder.Services.AddSingleton<IValidateOptions<MineCraftServerOptions>, MineCraftServerOptionsValidation>();
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<MineCraftServerOptions>>().Value);

            // Register services
            builder.Services.AddSingleton<IPreFlightCheckService, PreFlightCheckService>();
            builder.Services.AddSingleton<IMineCraftServerService, MineCraftServerService>();

            // Register update-related services
            builder.Services.AddSingleton<IMineCraftVersionService, MineCraftVersionService>();
            builder.Services.AddSingleton<IMineCraftUpdateDownloadService, MineCraftUpdateDownloadService>();
            builder.Services.AddSingleton<IMineCraftBackupService, MineCraftBackupService>();
            builder.Services.AddSingleton<IMineCraftUpdateService, MineCraftUpdateService>();

            // Register patch service
            builder.Services.AddSingleton<IMinecraftServerPatchService, MinecraftServerPatchService>();

            // Register auto-start service
            builder.Services.AddSingleton<IServerAutoStartService, ServerAutoStartService>();
            // Register scheduler service for time abstraction
            builder.Services.AddSingleton<IMineCraftSchedulerService, MineCraftSchedulerService>();

            // Register status provider first (before status service, since status service depends on it)
            var autoShutdownTimeExceededStatusHandler = new AutoShutdownTimeExceededStatusHandler();
            builder.Services.AddSingleton(sp =>
            {
                return new ServerStatusHandlers
                {
                    // Normal operation: delegate to the real status service (set later after IServerStatusService is created)
                    NormalStatusHandler = () => sp.GetRequiredService<IServerStatusService>().GetLifeCycleStateAsync(),

                    // Shutdown operation: return ShouldBeStopped once, then ShouldBeIdle
                    WindowsServiceShutdownStatusHandler = new ShutdownStatusHandler().GetStatusAsync,

                    AutoShutdownTimeExceededHandler = () => autoShutdownTimeExceededStatusHandler.GetStatusAsync()
                };
            });
            builder.Services.AddSingleton<IServerStatusProvider, ServerStatusProvider>();

            // Register status service (after status provider)
            builder.Services.AddSingleton<IServerStatusService, ServerStatusService>();

            // Register lifecycle service
            builder.Services.AddSingleton<IServerLifecycleService, ServerLifecycleService>();

            builder.Services.AddHostedService<MinecraftManagementWorkerService>();
        }
    }
}
