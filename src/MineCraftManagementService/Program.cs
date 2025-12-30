using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Services;
using MineCraftManagementService.Models;
using MineCraftManagementService.Validators;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var log = LogManager.GetLogger<Program>();

        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Configure NLog logging
            builder.Logging.AddNLogConfiguration();
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

            using var host = builder.Build();
            await host.RunAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopped via Windows Service Management or Ctrl+C
            log.Info("MineCraft Management Service stopped");
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Application exited with an error: {ex.GetType().Name}");
            Environment.Exit(1);
        }
        finally
        {
            NLog.LogManager.Shutdown();
        }
    }
}