using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace MineCraftManagementService.Services;

/// <summary>
/// Service for checking and cleaning up existing processes and port conflicts before server startup.
/// No exception handling - exceptions bubble to WindowsBackgroundService.
/// </summary>
public class PreFlightCheckService : IPreFlightCheckService
{
    private readonly ILog<PreFlightCheckService> _log;
    private readonly int[] _requiredPorts;

    public PreFlightCheckService(ILog<PreFlightCheckService> log, MineCraftServerOptions options)
    {
        _log = log;
        _requiredPorts = options.ServerPorts;
    }

    /// <summary>
    /// Checks for existing bedrock_server.exe process and TCP port conflicts.
    /// Terminates bedrock_server.exe if found. Throws an exception if ports are still in use.
    /// </summary>
    public async Task<bool> CheckAndCleanupAsync()
    {
        _log.Info("Starting preflight checks for existing processes and port conflicts...");

        bool processTerminated = await CheckAndTerminateExistingServerProcessAsync();
        await CheckAndTerminatePortConflictsAsync();

        if (processTerminated)
        {
            _log.Info("Preflight cleanup completed. Bedrock server process was found and terminated.");
            await Task.Delay(1000);
        }
        else
        {
            _log.Info("Preflight check complete. No bedrock_server.exe processes found.");
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
            _log.Debug("No existing bedrock_server.exe processes found.");
            return false;
        }

        _log.Warn($"Found {existingProcesses.Length} existing bedrock_server.exe process(es). Terminating...");

        foreach (var process in existingProcesses)
        {
            _log.Info($"Attempting to terminate bedrock_server.exe (PID: {process.Id})");
            process.Kill(true);
            _log.Info($"Successfully terminated bedrock_server.exe (PID: {process.Id})");
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
            _log.Debug($"TCP ports {string.Join(", ", _requiredPorts)} are available.");
            return;
        }

        var portList = string.Join(", ", conflictingPorts);
        _log.Error($"TCP ports {portList} are still in use after bedrock_server.exe termination.");

        throw new InvalidOperationException(
            $"Required TCP ports {portList} are in use by another process.");
    }
}
