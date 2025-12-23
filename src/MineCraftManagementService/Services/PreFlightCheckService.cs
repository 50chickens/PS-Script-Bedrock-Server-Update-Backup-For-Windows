using System.Diagnostics;
using System.Net.NetworkInformation;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

/// <summary>
/// Service for checking and cleaning up existing processes and port conflicts before server startup.
/// No exception handling - exceptions bubble to WindowsBackgroundService.
/// </summary>
public class PreFlightCheckService
{
    private readonly ILog<PreFlightCheckService> _logger;
    private readonly int[] _requiredPorts;

    public PreFlightCheckService(ILog<PreFlightCheckService> logger, MineCraftServerOptions options)
    {
        _logger = logger;
        _requiredPorts = options.ServerPorts;
    }

    /// <summary>
    /// Checks for existing bedrock_server.exe process and TCP port conflicts.
    /// Terminates bedrock_server.exe if found. Throws an exception if ports are still in use.
    /// </summary>
    public async Task<bool> CheckAndCleanupAsync()
    {
        _logger.Info("Starting preflight checks for existing processes and port conflicts...");
        
        bool processTerminated = await CheckAndTerminateExistingServerProcessAsync();
        await CheckAndTerminatePortConflictsAsync();

        if (processTerminated)
        {
            _logger.Info("Preflight cleanup completed. Bedrock server process was found and terminated.");
            await Task.Delay(1000);
        }
        else
        {
            _logger.Info("Preflight check complete. No bedrock_server.exe processes found.");
        }

        return processTerminated;
    }

    /// <summary>
    /// Checks for and terminates any existing bedrock_server.exe processes.
    /// </summary>
    private async Task<bool> CheckAndTerminateExistingServerProcessAsync()
    {
        var existingProcesses = Process.GetProcessesByName("bedrock_server");
        
        if (existingProcesses.Length == 0)
        {
            _logger.Debug("No existing bedrock_server.exe processes found.");
            return false;
        }

        _logger.Warn($"Found {existingProcesses.Length} existing bedrock_server.exe process(es). Terminating...");

        foreach (var process in existingProcesses)
        {
            _logger.Info($"Attempting to terminate bedrock_server.exe (PID: {process.Id})");
            process.Kill(true);
            _logger.Info($"Successfully terminated bedrock_server.exe (PID: {process.Id})");
        }

        return true;
    }

    /// <summary>
    /// Checks for TCP port conflicts and throws an exception if ports are still in use.
    /// This method assumes bedrock_server.exe has already been terminated.
    /// If ports are still in use, it indicates another process is holding them.
    /// </summary>
    private async Task CheckAndTerminatePortConflictsAsync()
    {
        var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
        var tcpConnections = ipGlobalProperties.GetActiveTcpConnections();

        var conflictingPorts = tcpConnections
            .Where(conn => _requiredPorts.Contains(conn.LocalEndPoint.Port))
            .Select(conn => conn.LocalEndPoint.Port)
            .Distinct()
            .ToList();

        if (conflictingPorts.Count == 0)
        {
            _logger.Debug($"TCP ports {string.Join(", ", _requiredPorts)} are available.");
            return;
        }

        var portList = string.Join(", ", conflictingPorts);
        _logger.Error($"TCP ports {portList} are still in use after bedrock_server.exe termination.");
        
        throw new InvalidOperationException(
            $"Required TCP ports {portList} are in use by another process.");
    }
}
