using MineCraftManagementService.Enums;
using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles the lifecycle management of the Minecraft server (start/stop).
/// </summary>
public class ServerLifecycleService : IServerLifecycleService
{
    private readonly ILog<ServerLifecycleService> _log;
    private readonly IMineCraftServerService _minecraftService;
    private readonly IPreFlightCheckService _preFlightCheckService;
    private readonly IServerStatusProvider _statusProvider;
    private readonly IMinecraftServerPatchService _patchService;
    private readonly IMineCraftBackupService _backupService;
    private readonly IServerAutoStartService _autoStartService;
    private readonly MineCraftServerOptions _options;

    public ServerLifecycleService(
        ILog<ServerLifecycleService> log,
        IMineCraftServerService minecraftService,
        IPreFlightCheckService preFlightCheckService,
        
        IServerStatusProvider statusProvider,
        IMinecraftServerPatchService patchService,
        IMineCraftBackupService backupService,
        IServerAutoStartService autoStartService,
        MineCraftServerOptions options)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _preFlightCheckService = preFlightCheckService ?? throw new ArgumentNullException(nameof(preFlightCheckService));
        _statusProvider = statusProvider ?? throw new ArgumentNullException(nameof(statusProvider));
        _patchService = patchService ?? throw new ArgumentNullException(nameof(patchService));
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
        await _preFlightCheckService.CheckAndCleanupAsync();

        await _autoStartService.ApplyAutoStartAsync(cancellationToken);

        await ManageLifecycleAsync(cancellationToken);
        _log.Info("Server lifecycle management finished. Server will not restart unless Management service is restarted.");
    }

    internal async Task ManageLifecycleAsync(CancellationToken cancellationToken)
    {
        int monitoringIntervalMs = _options.MonitoringIntervalSeconds * 1000;

        while (true)
        {
            try
            {
                var lifecycleState = await _statusProvider.GetLifeCycleStateAsync();
                _log.Trace($"Server lifecycle status: {lifecycleState.LifecycleStatus}");

                // Process server state
                switch (lifecycleState.LifecycleStatus)
                {
                    case MineCraftServerStatus.ShouldBeStarted:
                        await HandleStartServerAsync();
                        continue;

                    case MineCraftServerStatus.ShouldBeStopped:
                        await HandleStopServerAsync();
                        
                        continue;
                        
                    case MineCraftServerStatus.ShouldBePatched:
                        await HandlePatchServerAsync(lifecycleState.PatchVersion ?? "", cancellationToken);
                        continue;

                    case MineCraftServerStatus.ShouldBeMonitored:
                        await HandleServerMonitoringAsync(cancellationToken);
                        break;

                    case MineCraftServerStatus.ShouldBeIdle:
                        await HandleIdleStateAsync();
                        break;

                    case MineCraftServerStatus.Error:
                        _log.Error("Server encountered an error state");
                        break;

                    default:
                        _log.Warn($"Unknown server lifecycle status: {lifecycleState.LifecycleStatus}");
                        break;
                }

                await Task.Delay(monitoringIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _log.Info("Server lifecycle management cancelled");
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error during server lifecycle management");
            }
        }
    }

    private async Task HandleStartServerAsync()
    {
        if (!_minecraftService.IsRunning)
        {
            _log.Info("Starting Minecraft server");
            var success = await _minecraftService.StartServerAsync();
            if (!success)
            {
                _log.Error("Failed to start Minecraft server");
            }
        }
    }

    private async Task HandleStopServerAsync()
    {
        _log.Info("Stopping server...");
        if (!_minecraftService.IsRunning)
        {
            _log.Info("Server is already stopped.");
            return;
        }
        var wasShutdownGracefully = await _minecraftService.TryGracefulShutdownAsync();
        if (!wasShutdownGracefully)
        {
            _log.Warn("Graceful shutdown failed, forcing server stop");
            await _minecraftService.ForceStopServerAsync();
        }
        _log.Info("Server stopped successfully.");   
        _log.Debug("Waiting for ports to be released after server shutdown...");
        await Task.Delay(2000);
    }
    
    private async Task HandlePatchServerAsync(string patchVersion, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(patchVersion))
        {
            throw new InvalidOperationException("Patch version cannot be null or empty when applying patch");
        }

        _log.Info($"Applying server update patch to version {patchVersion}...");
        
        // Create backup before applying patch
        var backupPath = _backupService.CreateBackupZipFromServerFolder();
        _log.Info($"Backed up current installation to {backupPath}");
        
        // Apply the patch with the specific version
        await _patchService.ApplyUpdateAsync(patchVersion, cancellationToken);
        _log.Info("Patch applied successfully.");
    }

    private async Task HandleServerMonitoringAsync(CancellationToken cancellationToken)
    {
        // Server is running and up-to-date - monitor output in background task
        await Task.CompletedTask;
    }

    private async Task HandleIdleStateAsync()
    {
        // Server idle - EnableAutoStart disabled or auto-shutdown exceeded
        _log.Trace("Server is idle (auto-start disabled or awaiting manual intervention)");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the server gracefully and signals shutdown to the status provider.
    /// Prevents server restart by switching the status provider to shutdown mode.
    /// The actual shutdown is handled by the ManageLifecycleLoopAsync via HandleStopServerAsync.
    /// </summary>
    public async Task StopServerAsync()
    {
        _statusProvider.SetShutdownMode(ServerShutDownMode.WindowsServiceShutdown);
        
        // The ManageLifecycleLoopAsync will see ShouldBeStopped from ShutdownStatusHandler
        // and handle the actual shutdown via HandleStopServerAsync.
        // Wait with a timeout to ensure server stops.
        int maxWaitMs = 60000; // 60 seconds
        int checkIntervalMs = 100;
        int elapsedMs = 0;
        
        while (_minecraftService.IsRunning && elapsedMs < maxWaitMs)
        {
            await Task.Delay(checkIntervalMs);
            elapsedMs += checkIntervalMs;
        }
        
        if (_minecraftService.IsRunning)
        {
            _log.Warn("Server did not stop within timeout, forcing stop");
            await _minecraftService.ForceStopServerAsync();
        }
        _log.Info("Server shutdown process completed.");
    }
}
