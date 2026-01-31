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
    private readonly string _serverProcessName;

    public PreFlightCheckService(ILog<PreFlightCheckService> log, MineCraftServerOptions options)
    {
        _log = log;
        _requiredPorts = options.ServerPorts;
        // Extract process name from executable name (remove .exe extension)
        _serverProcessName = Path.GetFileNameWithoutExtension(options.ServerExecutableName);
    }

    /// <summary>
    /// Checks for existing bedrock_server.exe process and TCP port conflicts.
    /// Terminates bedrock_server.exe if found. Throws an exception if ports are still in use.
    /// </summary>
    public async Task<bool> CheckAndCleanupAsync()
    {
        _log.Info("Starting preflight checks for existing processes and port conflicts");

        bool processTerminated = await CheckAndTerminateExistingServerProcessAsync();
        await CheckAndTerminatePortConflictsAsync();

        if (processTerminated)
        {
            _log.Info($"Preflight cleanup completed - {_serverProcessName} process terminated");
            await Task.Delay(1000);
        }

        return processTerminated;
    }

    /// <summary>
    /// Checks for and terminates any existing bedrock_server.exe processes.
    /// </summary>
    private async Task<bool> CheckAndTerminateExistingServerProcessAsync()
    {
        var existingProcesses = Process.GetProcessesByName(_serverProcessName);

        if (existingProcesses.Length == 0)
        {
            return false;
        }

        _log.Warn($"Found {existingProcesses.Length} existing {_serverProcessName} process(es) - terminating");

        foreach (var process in existingProcesses)
        {
            try
            {
                _log.Info($"Terminating {_serverProcessName} (PID: {process.Id})");
                process.Kill(true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                _log.Warn($"Failed to terminate process {process.Id} - may have already exited. Error: {ex.Message}");
            }
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
            return;
        }

        var portList = string.Join(", ", conflictingPorts);
        _log.Error($"Required TCP ports {portList} are in use by another process after {_serverProcessName} termination");

        throw new InvalidOperationException(
            $"Required TCP ports {portList} are in use by another process.");
    }
}
