using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles the auto-start logic for the Minecraft server.
/// </summary>
public class ServerAutoStartService : IServerAutoStartService
{
    private readonly ILog<ServerAutoStartService> _logger;
    private readonly IMineCraftServerService _minecraftService;
    private readonly MineCraftServerOptions _options;

    public ServerAutoStartService(
        ILog<ServerAutoStartService> logger,
        IMineCraftServerService minecraftService,
        MineCraftServerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _minecraftService = minecraftService ?? throw new ArgumentNullException(nameof(minecraftService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Applies the auto-start configuration if enabled.
    /// Waits for the configured delay and then starts the server.
    /// </summary>
    public async Task ApplyAutoStartAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableAutoStart)
        {
            return;
        }

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
