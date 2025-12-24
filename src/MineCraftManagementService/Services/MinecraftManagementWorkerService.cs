using Microsoft.Extensions.Hosting;
using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;

namespace MineCraftManagementService.Services;

public class MinecraftManagementWorkerService : BackgroundService
{
    private readonly ILog<MinecraftManagementWorkerService> _log;
    private readonly IServerLifecycleService _lifecycleService;

    public MinecraftManagementWorkerService(
        ILog<MinecraftManagementWorkerService> logger,
        IServerLifecycleService lifecycleService)
    {
        _log = logger ?? throw new ArgumentNullException(nameof(logger));
        _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _log.Info("MineCraft Management Service starting...");
        await base.StartAsync(cancellationToken);
        _log.Info("MineCraft Management Service started successfully");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.Info("MineCraft Management Service stopping...");
        await _lifecycleService.StopServerAsync();
        // Wait for server to fully shut down before exiting service
        await Task.Delay(1000);
        await base.StopAsync(cancellationToken);
        _log.Info("MineCraft Management Service stopped");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Info("MineCraft Management Service background task running");
        try
        {
            _log.Info("Starting server lifecycle management...");
            await _lifecycleService.ManageServerLifecycleAsync(stoppingToken);
            _log.Info("MineCraft Management Service monitoring loop completed normally");
        }
        catch (OperationCanceledException)
        {
            _log.Info("MineCraft Management Service cancelled");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "MineCraft Management Service encountered an error during monitoring");
            Environment.Exit(1);
        }
    }
}
