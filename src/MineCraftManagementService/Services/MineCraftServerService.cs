using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using System.Diagnostics;

namespace MineCraftManagementService.Services;

/// <summary>
/// Manages Minecraft server process lifecycle and state.
/// </summary>
public class MineCraftServerService : IMineCraftServerService
{
    private readonly ILog<MineCraftServerService> _log;
    private readonly MineCraftServerOptions _options;
    private Process? _serverProcess;
    private DateTime _serverStartTime = DateTime.MinValue;
    private string _currentVersion = "unknown";

    private int _serverProcessId
    {
        get
        {
            if (_serverProcess is null)
                return -1;
            return _serverProcess.Id;
        }
    }
    private bool _isRunning;
    private readonly object _processLock = new object();

    public bool IsRunning
    {
        get
        {
            if (!_isRunning || _serverProcess is null)
                return false;

            return !_serverProcess.HasExited;
        }
    }

    public DateTime ServerStartTime => _serverStartTime;

    /// <summary>
    /// Gets the current server version from VERSION.TXT or defaults to "unknown".
    /// </summary>
    public string CurrentVersion
    {
        get
        {
            // Refresh version on each access to ensure we have the latest
            _currentVersion = GetCurrentVersionFromFiles();
            return _currentVersion;
        }
    }

    public MineCraftServerService(ILog<MineCraftServerService> log, MineCraftServerOptions options)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _isRunning = false;
        _currentVersion = GetCurrentVersionFromFiles();
    }

    /// <summary>
    /// Starts the Minecraft server process.
    /// </summary>
    public async Task<bool> StartServerAsync()
    {
        if (IsRunning)
        {
            _log.Warn("Minecraft server is already running.");
            return true;
        }

        _log.Info($"Starting Minecraft server from: {_options.ServerPath}");

        var serverExecutablePath = Path.Combine(_options.ServerPath, _options.ServerExecutableName);

        if (!File.Exists(serverExecutablePath))
        {
            _log.Error($"Server executable not found at: {serverExecutablePath}");
            return false;
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = serverExecutablePath,
            WorkingDirectory = _options.ServerPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _serverProcess = Process.Start(processStartInfo)!;
        _serverStartTime = DateTime.Now;
        _isRunning = true;
        _log.Info($"Minecraft server started successfully with PID: {_serverProcess.Id}");

        _ = MonitorProcessOutputAsync();

        await Task.Delay(3000);

        return IsRunning;
    }
    /// <summary>
    /// Checks for the Minecraft server process by ID first, then by name if ID check fails.
    /// This method combines both checks into a single atomic operation to avoid race conditions.
    /// </summary>
    /// <returns>True if process was found and attached, false otherwise.</returns>
    private bool CheckForProcess()
    {
        lock (_processLock)
        {
            int processId = _serverProcessId;
            
            // First, try to find by stored process ID
            if (processId >= 0 && ProcessExists(processId))
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    if (!process.HasExited)
                    {
                        _log.Info($"Found process with stored process Id: {processId}");
                        _serverProcess = process;
                        _isRunning = true;
                        return true;
                    }
                }
                catch (ArgumentException)
                {
                    // Process no longer exists
                    _log.Debug("Process with stored ID no longer exists");
                }
            }
            
            // If process ID check failed, try finding by process name
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_options.ServerExecutableName));
            if (processes.Length > 0)
            {
                var process = processes.Where(p => !p.HasExited).FirstOrDefault();
                if (process != null)
                {
                    _log.Info($"Found process with name {_options.ServerExecutableName} and PID: {process.Id}");
                    _serverProcess = process;
                    _isRunning = true;
                    return true;
                }
            }

            _log.Info("Could not find Minecraft server process by either ID or name.");
            _serverProcess = null;
            _isRunning = false;
            return false;
        }
    }
    /// <summary>
    /// Attempts to gracefully shut down the Minecraft server by sending the "/stop" command via stdin.
    /// Returns true only if the process actually terminates after sending the command.
    /// </summary>
    public async Task<bool> TryGracefulShutdownAsync()
    {
        // If we have the original process reference and it's still running, use it directly
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _log.Info("Using stored process reference for graceful shutdown");
        }
        else
        {
            _log.Info("Stored process reference invalid, attempting to locate process...");

            bool foundProcess = CheckForProcess();
            if (!foundProcess)
            {
                _log.Info("Could not find Minecraft server process.");
                return true;
            }
            else
            {
                _log.Info("Found server process and attached to it.");
            }
        }

        _log.Info("Attempting graceful shutdown by sending 'stop' command to server stdin");

        if (_serverProcess?.StandardInput != null)
        {
            _serverProcess.StandardInput.Write($"{_options.GracefulShutdownCommand}\r\n");
            _serverProcess.StandardInput.Flush();
            _log.Info($"Sent '{_options.GracefulShutdownCommand}' command to server stdin, waiting for process termination...");
        }
        else
        {
            _log.Warn("StandardInput not available for graceful shutdown (process was not started with stdin redirection), will force stop server");
            return false;
        }

        int maxWaitMs = _options.GracefulShutdownMaxWaitSeconds * 1000;
        int checkIntervalMs = _options.GracefulShutdownCheckIntervalMs;
        int elapsedMs = 0;

        while (elapsedMs < maxWaitMs)
        {
            if (!ProcessExists(_serverProcessId))
            {
                _log.Info($"Server process {_serverProcessId} stopped gracefully.");
                _isRunning = false;
                return true;
            }

            await Task.Delay(checkIntervalMs);
            elapsedMs += checkIntervalMs;
        }

        _log.Warn($"Server process {_serverProcessId} did not terminate within {maxWaitMs}ms after graceful shutdown attempt");
        return false;
    }

    /// <summary>
    /// Stops the Minecraft server process forcefully.
    /// </summary>
    public async Task<bool> ForceStopServerAsync()
    {
        if (!IsRunning)
        {
            _log.Warn("Minecraft server is not running.");
            return true;
        }

        if (_serverProcess is null)
        {
            _log.Warn("Server process is null");
            _isRunning = false;
            return false;
        }

        var processId = _serverProcess.Id;
        _log.Info($"Stopping Minecraft server (PID: {processId})");

        // Force kill the process
        if (!_serverProcess.HasExited)
        {
            _log.Info("Force killing Bedrock server process");
            _serverProcess.Kill(true);

            int stopTimeoutMs = _options.StopTimeoutSeconds * 1000;
            var killTask = Task.Run(() => _serverProcess.WaitForExit(stopTimeoutMs));
            await Task.WhenAny(killTask, Task.Delay(stopTimeoutMs));
        }

        if (!ProcessExists(processId))
        {
            _isRunning = false;
            _log.Info("Minecraft server stopped successfully");
            return true;
        }
        else
        {
            _log.Error("Failed to terminate server process");
            _isRunning = false;
            return false;
        }
    }

    /// <summary>
    /// Checks if a process with the given ID exists.
    /// </summary>
    private bool ProcessExists(int processId)
    {
        try
        {
            Process.GetProcessById(processId);
            return true;
        }
        catch (ArgumentException)
        {
            // Process doesn't exist - this is an expected condition, not an error
            _log.Debug($"Process {processId} does not exist");
            return false;
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Unexpected error checking if process {processId} exists");
            return false;
        }
    }

    /// <summary>
    /// Gets the current server version from the extracted version in the output.
    /// Returns "unknown" if version has not been extracted from server output yet.
    /// </summary>
    private string GetCurrentVersionFromFiles()
    {
        return _currentVersion;
    }

    private async Task MonitorProcessOutputAsync()
    {
        if (_serverProcess is null)
            return;

        using var cts = new CancellationTokenSource(TimeSpan.FromHours(1));

        while (!_serverProcess.HasExited && !cts.Token.IsCancellationRequested)
        {
            var line = await _serverProcess.StandardOutput.ReadLineAsync(cts.Token);
            if (!string.IsNullOrEmpty(line))
            {
                _log.Info($"{line}");

                if (line.Contains("Version:"))
                {
                    ExtractVersionFromOutput(line);
                }
            }
            else
            {
                break;
            }
        }

        if (_serverProcess.HasExited)
        {
            if (_serverProcess.ExitCode != 0)
            {
                _log.Warn($"Server exited with code: {_serverProcess.ExitCode}");
            }
            _isRunning = false;
        }
    }

    /// <summary>
    /// Extracts the version number from server output line containing "Version: X.X.X.X".
    /// </summary>
    private void ExtractVersionFromOutput(string outputLine)
    {
        try
        {
            var versionIndex = outputLine.IndexOf("Version:");
            if (versionIndex >= 0)
            {
                var versionPart = outputLine.Substring(versionIndex + "Version:".Length).Trim();
                var version = versionPart.Split(new[] { ' ', '[', ']' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(version))
                {
                    _currentVersion = version;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to extract version from output");
        }
    }

    public void Dispose()
    {
        _serverProcess?.Dispose();
    }

}
