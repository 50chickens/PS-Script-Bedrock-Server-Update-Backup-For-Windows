namespace MineCraftManagementService.Interfaces;

/// <summary>
/// Service responsible for applying auto-start configuration to the Minecraft server.
/// </summary>
public interface IServerAutoStartService
{
    /// <summary>
    /// Applies the auto-start configuration if enabled.
    /// If auto-start is disabled, this does nothing.
    /// If auto-start is enabled, waits for the configured delay and then starts the server.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    Task ApplyAutoStartAsync(CancellationToken cancellationToken = default);
}
