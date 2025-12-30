using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Handles the auto-start logic for the Minecraft server.
/// </summary>
public class ServerAutoStartService : IServerAutoStartService
{
    private readonly ILog<ServerAutoStartService> _log;
    private readonly IMineCraftServerService _minecraftService;
    private readonly MineCraftServerOptions _options;

    public ServerAutoStartService(
        ILog<ServerAutoStartService> log,
        IMineCraftServerService minecraftService,
        MineCraftServerOptions options)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
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

        _log.Info($"Auto-starting Minecraft server with {_options.AutoStartDelaySeconds} second delay");
        await Task.Delay(_options.AutoStartDelaySeconds * 1000, cancellationToken);
        
        var success = await _minecraftService.StartServerAsync();
        if (success)
        {
            _log.Info("Minecraft server auto-started successfully");
        }
        else
        {
            _log.Error("Failed to auto-start Minecraft server");
        }
    }
}
