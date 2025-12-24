using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles the lifecycle management of the Minecraft server (start/stop).
/// </summary>
public class ServerLifecycleService : IServerLifecycleService
{
    private readonly ILog<ServerLifecycleService> _logger;
    private readonly IMineCraftServerService _minecraftService;
    private readonly IPreFlightCheckService _preFlightCheckService;
    private readonly IServerStatusService _statusService;
    private readonly IServerStatusProvider _statusProvider;
    private readonly IMinecraftServerPatchService _patchService;
    private readonly IMineCraftUpdateService _updateService;
    private readonly IMineCraftBackupService _backupService;
    private readonly IServerAutoStartService _autoStartService;
    private readonly MineCraftServerOptions _options;

    public ServerLifecycleService(
        ILog<ServerLifecycleService> logger,
        IMineCraftServerService minecraftService,
        IPreFlightCheckService preFlightCheckService,
        IServerStatusService statusService,
        IServerStatusProvider statusProvider,
        IMinecraftServerPatchService patchService,
        IMineCraftUpdateService updateService,
        IMineCraftBackupService backupService,
        IServerAutoStartService autoStartService,
        MineCraftServerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _preFlightCheckService = preFlightCheckService ?? throw new ArgumentNullException(nameof(preFlightCheckService));
        _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
        _statusProvider = statusProvider ?? throw new ArgumentNullException(nameof(statusProvider));
        _patchService = patchService ?? throw new ArgumentNullException(nameof(patchService));
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _autoStartService = autoStartService ?? throw new ArgumentNullException(nameof(autoStartService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Manages the server lifecycle by monitoring status and handling state transitions.
    /// Runs a continuous loop that gets status and processes state changes via switch statement.
    /// </summary>
    public async Task ManageServerLifecycleAsync(CancellationToken cancellationToken = default)
    {
        // Run preflight checks to clean up any existing bedrock_server processes
        await _preFlightCheckService.CheckAndCleanupAsync();

        // Apply auto-start configuration
        await _autoStartService.ApplyAutoStartAsync(cancellationToken);

        // Monitor loop
        await ManageLifecycleLoopAsync(cancellationToken);
    }

    internal async Task ManageLifecycleLoopAsync(CancellationToken cancellationToken)
    {
        int monitoringIntervalMs = _options.MonitoringIntervalSeconds * 1000;

        while (true)
        {
            try
            {
                var status = await _statusProvider.GetStatusAsync();
                _logger.Trace($"Server status: {status}");

                // Process server state
                switch (status)
                {
                    case MineCraftServerStatus.ShouldBeStarted:
                        await HandleStartServerAsync();
                        // Recheck status immediately to handle any state changes
                        continue;

                    case MineCraftServerStatus.ShouldBeStopped:
                        await HandleStopServerAsync();
                        
                        // Recheck status to determine if stopping for patch or auto-shutdown
                        // ServerStatusService will return ShouldBePatched if update available, ShouldBeIdle if auto-start disabled
                        continue;
                        
                    case MineCraftServerStatus.ShouldBePatched:
                        await HandlePatchServerAsync(cancellationToken);
                        // Recheck status after patch to see if server should be started or if another patch is needed
                        continue;

                    case MineCraftServerStatus.ShouldBeMonitored:
                        await HandleServerMonitoringAsync(cancellationToken);
                        break;

                    case MineCraftServerStatus.ShouldBeIdle:
                        await HandleIdleStateAsync();
                        break;

                    case MineCraftServerStatus.Error:
                        _logger.Error("Server encountered an error state");
                        break;

                    default:
                        _logger.Warn($"Unknown server status: {status}");
                        break;
                }

                await Task.Delay(monitoringIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Server lifecycle management cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during server lifecycle management");
            }
        }
    }

    private async Task HandleStartServerAsync()
    {
        if (!_minecraftService.IsRunning)
        {
            _logger.Info("Starting Minecraft server");
            var success = await _minecraftService.StartServerAsync();
            if (!success)
            {
                _logger.Error("Failed to start Minecraft server");
            }
        }
    }

    private async Task HandleStopServerAsync()
    {
        _logger.Info("Stopping server...");
        
        var wasShutdownGracefully = await _minecraftService.TryGracefulShutdownAsync();
        if (!wasShutdownGracefully)
        {
            _logger.Warn("Graceful shutdown failed, forcing server stop");
            await _minecraftService.ForceStopServerAsync();
        }
    }
    
    private async Task HandlePatchServerAsync(CancellationToken cancellationToken)
    {
        _logger.Info("Applying server update patch...");
        
        // Server should already be stopped at this point, no need to shutdown again
        // ShouldBePatched already confirmed an update is available, so no need to check again
        
        // Create backup after successful server stop
        var backupPath = _backupService.CreateBackupZipFromServerFolder();
        _logger.Info($"Backed up current installation to {backupPath}");
        
        // Apply the patch - the patch service will handle getting the latest version
        await _patchService.ApplyUpdateAsync(null, cancellationToken);
        _logger.Info("Patch applied successfully. Next cycle will start the server.");
    }

    private async Task HandleServerMonitoringAsync(CancellationToken cancellationToken)
    {
        // When server is running and up to date, we just monitor it.
        // The MonitorProcessOutputAsync in MineCraftServerService handles logging server output
        // in a separate background task.
        // This state simply indicates the server is healthy and requires monitoring.
        await Task.CompletedTask;
    }

    private async Task HandleIdleStateAsync()
    {
        // Server is idle: either EnableAutoStart is disabled or auto-shutdown has exceeded.
        // In this state, we do nothing and wait for EnableAutoStart to be re-enabled or manual intervention.
        _logger.Trace("Server is idle (auto-start disabled or awaiting manual intervention)");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the server gracefully and signals shutdown to the status provider.
    /// Prevents server restart by switching the status provider to shutdown mode.
    /// </summary>
    public async Task StopServerAsync()
    {
        _statusProvider.SetShutdownMode();
        var wasShutdownGracefully = await _minecraftService.TryGracefulShutdownAsync();
        if (!wasShutdownGracefully)
        {
            _logger.Warn("Graceful shutdown failed, forcing server stop");
            await _minecraftService.ForceStopServerAsync();
        }
    }
}
