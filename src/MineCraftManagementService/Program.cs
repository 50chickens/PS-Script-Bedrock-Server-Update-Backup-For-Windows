using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using NLog;
using NLog.Extensions.Logging;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Options;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Services;
using MineCraftManagementService.Models;
using MineCraftManagementService;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Configure NLog programmatically
        MineCraftManagementService.Logging.NLogConfigurator.Configure();

        var logger = LogManager.GetCurrentClassLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Configure the app to work as a Windows Service
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "MineCraft Management Service";
            });

            // Register EventLog provider options
            LoggerProviderOptions.RegisterProviderOptions<
                EventLogSettings, EventLogLoggerProvider>(builder.Services);

            // Register custom ILog<T> factory for dependency injection
            builder.Services.AddSingleton(typeof(ILog<>), typeof(NLogLoggerCore<>));

            // Add configuration for environment
            builder.Configuration.AddEnvironmentVariables(prefix: "MINECRAFT_");

            // Add app settings for Minecraft service configuration
            builder.Services.AddOptions<MineCraftServerOptions>()
                .Bind(builder.Configuration.GetSection(MineCraftServerOptions.Settings))
                .ValidateOnStart();
            builder.Services.AddSingleton<IValidateOptions<MineCraftServerOptions>, MineCraftServerOptionsValidation>();
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<MineCraftServerOptions>>().Value);

            // Register services
            builder.Services.AddSingleton<PreFlightCheckService>();
            builder.Services.AddSingleton<MineCraftServerService>();
            builder.Services.AddSingleton<ServerLifecycleService>();
            
            // Register update-related services
            builder.Services.AddSingleton<MineCraftUpdateDownloaderService>();
            builder.Services.AddSingleton<MineCraftBackupService>();
            builder.Services.AddSingleton<MineCraftUpdateService>();
            
            // Register patch service with dependencies
            builder.Services.AddSingleton(sp => 
            {
                return new MinecraftServerPatchService(
                    sp.GetRequiredService<ILog<MinecraftServerPatchService>>(),
                    sp.GetRequiredService<MineCraftServerService>(),
                    sp.GetRequiredService<MineCraftUpdateDownloaderService>(),
                    sp.GetRequiredService<MineCraftBackupService>(),
                    sp.GetRequiredService<MineCraftServerOptions>()
                );
            });
            
            builder.Services.AddSingleton<ServerMonitoringService>();
            builder.Services.AddHostedService<WindowsBackgroundService>();

            using var host = builder.Build();
            await host.RunAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopped via Windows Service Management or Ctrl+C
            logger.Info("MineCraft Management Service stopped");
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Application exited with an error: {ex.GetType().Name}");
            Environment.Exit(1);
        }
        finally
        {
            LogManager.Shutdown();
        }
    }
}