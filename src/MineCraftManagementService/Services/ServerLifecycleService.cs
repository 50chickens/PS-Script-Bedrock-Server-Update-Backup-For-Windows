using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles the lifecycle management of the Minecraft server (start/stop).
/// </summary>
public class ServerLifecycleService
{
    private readonly ILog<ServerLifecycleService> _logger;
    private readonly MineCraftServerService _minecraftService;
    private readonly PreFlightCheckService _preFlightCheckService;
    private readonly MineCraftServerOptions _options;

    public ServerLifecycleService(
        ILog<ServerLifecycleService> logger,
        MineCraftServerService minecraftService,
        PreFlightCheckService preFlightCheckService,
        MineCraftServerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _preFlightCheckService = preFlightCheckService ?? throw new ArgumentNullException(nameof(preFlightCheckService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Starts the server with preflight checks and configured auto-start delay if enabled.
    /// </summary>
    public async Task StartServerWithConfigAsync(CancellationToken cancellationToken = default)
    {
        // Run preflight checks to clean up any existing bedrock_server processes
        await _preFlightCheckService.CheckAndCleanupAsync();

        if (_options.EnableAutoStart)
        {
            _logger.Info($"Auto-starting Minecraft server with {_options.AutoStartDelaySeconds} second delay");
            await Task.Delay(_options.AutoStartDelaySeconds * 1000, cancellationToken);

            var success = await _minecraftService.StartServerAsync();
            if (success)
            {
                _logger.Info("Minecraft server auto-started successfully");
            }
            else
            {
                _logger.Error("Failed to auto-start Minecraft server");
            }
        }
    }

    /// <summary>
    /// Stops the server gracefully.
    /// </summary>
    public async Task StopServerAsync()
    {
        var wasShutdownGracefully = await _minecraftService.TryGracefulShutdownAsync();
        if (!wasShutdownGracefully)
        {
            _logger.Warn("Graceful shutdown failed, forcing server stop");
            await _minecraftService.ForceStopServerAsync();
        }
    }
}
