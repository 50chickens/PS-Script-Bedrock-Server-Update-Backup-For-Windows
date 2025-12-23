using System.Diagnostics;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Services;

public partial class MineCraftServerService
{
    private readonly ILog<MineCraftServerService> _logger;
    private readonly MineCraftServerOptions _options;
    private Process? _serverProcess;
    private DateTime _serverStartTime = DateTime.MinValue;
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

    public MineCraftServerService(ILog<MineCraftServerService> logger, MineCraftServerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _isRunning = false;
    }

    /// <summary>
    /// Starts the Minecraft server process.
    /// </summary>
    public async Task<bool> StartServerAsync()
    {
        if (IsRunning)
        {
            _logger.Warn("Minecraft server is already running.");
            return true;
        }

        _logger.Info($"Starting Minecraft server from: {_options.ServerPath}");

        var serverExecutablePath = Path.Combine(_options.ServerPath, _options.ServerExecutableName);

        if (!File.Exists(serverExecutablePath))
        {
            _logger.Error($"Server executable not found at: {serverExecutablePath}");
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
        _serverStartTime = DateTime.UtcNow;
        _isRunning = true;
        _logger.Info($"Minecraft server started successfully with PID: {_serverProcess.Id}");

        // Monitor output
        _ = MonitorProcessOutputAsync();

        // Give server time to initialize
        await Task.Delay(1000);

        return IsRunning;
    }
    public bool CheckForProcessByName()
    {
        //look for process by name using options.ServerExecutableName
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_options.ServerExecutableName));
        if (processes.Length > 0)
        {
            var process = processes.Where(p => !p.HasExited).FirstOrDefault(); //it's possible that multiple processes exist with the same name. todo: fix that.
            _logger.Info($"Found process with name {_options.ServerExecutableName} and PID: {process.Id}");
            _serverProcess = process;
            _isRunning = true;
            return true;
        }
        else
        {
            _logger.Info($"No process found with name {_options.ServerExecutableName}");
            return false;
        }
    }
    public bool CheckForProcessbyProcessId()
    {
        int processId = _serverProcessId;
        if (processId >= 0)
        {
            var processExists = ProcessExists(processId);
            if (processExists)
            {
                var process = Process.GetProcessById(processId);
                _logger.Info($"Found process with stored process Id: {processId}");
                _serverProcess = process;
                _isRunning = true;
                return true;
            }
            else
            {
                _logger.Info("No process found with the stored process Id.");
                return false;
            }
        }
        else
        {
            _logger.Info("Stored process Id is not valid.");
            _serverProcess = null; 
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
        // (it has redirected stdin from when we started it)
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _logger.Info("Using stored process reference for graceful shutdown");
        }
        else
        {
            // Otherwise, try to find the process by ID or name
            _logger.Info("Stored process reference invalid, attempting to locate process...");
            
            bool foundProcessByProcessId = CheckForProcessbyProcessId();
            if (foundProcessByProcessId)
            {
                _logger.Info("Found server process by stored process Id.");
            }
            else
            {
                bool foundProcessByName = CheckForProcessByName();
                if (foundProcessByName)
                {
                    _logger.Info("Process id was not valid, but found server process by executable name.");
                }
                else
                {
                    _logger.Info("Could not find Minecraft server process by either name, or id.");
                    return true;
                }
            }
        }

        _logger.Info("Attempting graceful shutdown by sending 'stop' command to server stdin");

        // Write "stop" command to the server's standard input with proper line termination
        if (_serverProcess?.StandardInput != null)
        {
            _serverProcess.StandardInput.Write($"{_options.GracefulShutdownCommand}\r\n");
            _serverProcess.StandardInput.Flush();
            _logger.Info($"Sent '{_options.GracefulShutdownCommand}' command to server stdin, waiting for process termination...");
        }
        else
        {
            _logger.Warn("StandardInput not available for graceful shutdown (process was not started with stdin redirection), will force stop server");
            return false;
        }

        // Wait for the process to terminate (up to configured timeout)
        int maxWaitMs = _options.GracefulShutdownMaxWaitSeconds * 1000;
        int checkIntervalMs = _options.GracefulShutdownCheckIntervalMs;
        int elapsedMs = 0;

        while (elapsedMs < maxWaitMs)
        {
            if (!ProcessExists(_serverProcessId))
            {
                _logger.Info($"Server process {_serverProcessId} terminated successfully after graceful shutdown");
                _isRunning = false;
                return true;
            }

            await Task.Delay(checkIntervalMs);
            elapsedMs += checkIntervalMs;
        }

        _logger.Warn($"Server process {_serverProcessId} did not terminate within {maxWaitMs}ms after graceful shutdown attempt");
        return false;
    }

    /// <summary>
    /// Stops the Minecraft server process forcefully.
    /// </summary>
    public async Task<bool> ForceStopServerAsync()
    {
        if (!IsRunning)
        {
            _logger.Warn("Minecraft server is not running.");
            return true;
        }

        if (_serverProcess is null)
        {
            _logger.Warn("Server process is null");
            _isRunning = false;
            return false;
        }

        var processId = _serverProcess.Id;
        _logger.Info($"Stopping Minecraft server (PID: {processId})");

        // Force kill the process
        if (!_serverProcess.HasExited)
        {
            _logger.Info("Force killing Bedrock server process");
            _serverProcess.Kill(true); // Kill the process and all child processes
            
            // Wait for process to exit using configured stop timeout
            int stopTimeoutMs = _options.StopTimeoutSeconds * 1000;
            var killTask = Task.Run(() => _serverProcess.WaitForExit(stopTimeoutMs));
            await Task.WhenAny(killTask, Task.Delay(stopTimeoutMs));
        }

        // Verify process is actually gone
        if (!ProcessExists(processId))
        {
            _isRunning = false;
            _logger.Info("Minecraft server stopped successfully");
            return true;
        }
        else
        {
            _logger.Error("Failed to terminate server process");
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
            return false;
        }
    }

    /// <summary>
    /// Restarts the Minecraft server.
    /// </summary>
    public async Task<bool> RestartServerAsync()
    {
        _logger.Info("Restarting Minecraft server");

        var wasShutdownGracefully = await TryGracefulShutdownAsync();
        if (!wasShutdownGracefully)
        {
            _logger.Warn("Graceful shutdown failed, forcing server stop");
            await ForceStopServerAsync();
        }
        // Wait a bit before starting again
        await Task.Delay(2000);

        return await StartServerAsync();
    }
    /// <summary>
    /// Gets the server status.
    /// </summary>
    public MineCraftServerStatus GetStatus()
    {
        if (!IsRunning)
        {
            return MineCraftServerStatus.NotRunning;
        }

        return MineCraftServerStatus.Running;
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
                _logger.Info($"[SERVER OUTPUT] {line}");
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
                _logger.Warn($"Server exited with code: {_serverProcess.ExitCode}");
            }
            _isRunning = false;
        }
    }

    public void Dispose()
    {
        _serverProcess?.Dispose();
    }

}
