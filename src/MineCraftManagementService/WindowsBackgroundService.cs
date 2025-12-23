using Microsoft.Extensions.Hosting;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;

namespace MineCraftManagementService;

/// <summary>
/// Background service that runs as a Windows Service and manages the Minecraft server lifecycle.
/// Delegates lifecycle and monitoring responsibilities to specialized services.
/// </summary>
public class WindowsBackgroundService : BackgroundService
{
    private readonly ILog<WindowsBackgroundService> _log;
    private readonly ServerLifecycleService _lifecycleService;
    private readonly ServerMonitoringService _monitoringService;
    private readonly MineCraftUpdateService _updateCheckService;
    private readonly MinecraftServerPatchService _patchService;
    private readonly MineCraftServerOptions _options;

    public WindowsBackgroundService(
        ILog<WindowsBackgroundService> logger,
        ServerLifecycleService lifecycleService,
        ServerMonitoringService monitoringService,
        MineCraftUpdateService updateCheckService,
        MinecraftServerPatchService patchService,
        MineCraftServerOptions options)
    {
        _log = logger ?? throw new ArgumentNullException(nameof(logger));
        _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
        _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
        _updateCheckService = updateCheckService ?? throw new ArgumentNullException(nameof(updateCheckService));
        _patchService = patchService ?? throw new ArgumentNullException(nameof(patchService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
        await base.StopAsync(cancellationToken);
        _log.Info("MineCraft Management Service stopped");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Info("MineCraft Management Service background task running");
        try
        {
            // First, start the server with preflight checks and auto-start logic
            _log.Info("Performing server startup initialization...");
            await _lifecycleService.StartServerWithConfigAsync(stoppingToken);
            _log.Info("Server startup initialization complete");

            // Then monitor the server and periodically check for updates
            _log.Info("Starting server monitoring...");
            await _monitoringService.MonitorServerAsync(stoppingToken);
            _log.Info("MineCraft Management Service monitoring loop completed normally");
        }
        catch (OperationCanceledException ex)
        {
            _log.Debug(ex, "MineCraft Management Service monitoring cancelled by host");
            // When the stopping token is canceled, for example, a call made from services.msc,
            // we shouldn't exit with a non-zero exit code. In other words, this is expected...
        }
        catch (Exception ex)
        {
            _log.Error(ex, "MineCraft Management Service encountered an error during monitoring");
            // Terminates this process and returns an exit code to the operating system.
            // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
            // performs one of two scenarios:
            // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
            // 2. When set to "StopHost": will cleanly stop the host, and log errors.
            //
            // In order for the Windows Service Management system to leverage configured
            // recovery options, we need to terminate the process with a non-zero exit code.
            Environment.Exit(1);
        }
    }
}
