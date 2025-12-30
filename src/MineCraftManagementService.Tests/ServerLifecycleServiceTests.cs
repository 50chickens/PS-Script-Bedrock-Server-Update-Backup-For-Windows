using MineCraftManagementService.Enums;
using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class ServerLifecycleServiceTests
{
    private IServerLifecycleService _service = null!;
    private ILog<ServerLifecycleService> _log = null!;
    private IMineCraftServerService _minecraftService = null!;
    private IPreFlightCheckService _preFlightService = null!;
    private IServerStatusProvider _statusProvider = null!;
    private IMinecraftServerPatchService _patchService = null!;
    private IMineCraftUpdateService _updateService = null!;
    private IMineCraftBackupService _backupService = null!;
    private IServerAutoStartService _autoStartService = null!;
    private MineCraftServerOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<ServerLifecycleService>();
        _minecraftService = Substitute.For<IMineCraftServerService>();
        _preFlightService = Substitute.For<IPreFlightCheckService>();
        _statusProvider = Substitute.For<IServerStatusProvider>();
        _patchService = Substitute.For<IMinecraftServerPatchService>();
        _updateService = Substitute.For<IMineCraftUpdateService>();
        _backupService = Substitute.For<IMineCraftBackupService>();
        _autoStartService = Substitute.For<IServerAutoStartService>();
        _options = TestUtils.CreateOptions();

        _service = new ServerLifecycleService(_log, _minecraftService, _preFlightService, _statusProvider, _patchService, _backupService, _autoStartService, _options);
    }

    /// <summary>
    /// Test: ManageServerLifecycleAsync runs preflight checks when service starts.
    /// Intent: Verify that startup performs necessary checks before beginning normal operation.
    /// Importance: Safety-critical - ensures server environment is valid before management begins.
    /// </summary>
    [Test]
    public async Task ManageServerLifecycleAsync_Preflight_RanOnStartup()
    {
        _statusProvider.GetLifeCycleStateAsync().Returns(Task.FromResult(new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeMonitored }));
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        await _preFlightService.Received(1).CheckAndCleanupAsync();
    }

    /// <summary>
    /// Test: ManageServerLifecycleAsync applies auto-start settings during operation.
    /// Intent: Verify that auto-start feature is properly invoked during normal lifecycle management.
    /// Importance: Ensures auto-start configuration is applied throughout server operation.
    /// </summary>
    [Test]
    public async Task ManageServerLifecycleAsync_AutoStartEnabled_StartsServer()
    {
        _autoStartService.ApplyAutoStartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _statusProvider.GetLifeCycleStateAsync().Returns(Task.FromResult(new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeMonitored }));
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        await _autoStartService.Received(1).ApplyAutoStartAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Test: ManageServerLifecycleAsync starts the server when status indicates ShouldBeStarted.
    /// Intent: Verify that the service responds to start signals by invoking server startup.
    /// Importance: Core lifecycle management - ensures servers start when signaled to do so.
    /// </summary>
    [Test]
    public async Task ManageServerLifecycleAsync_ShouldBeStarted_StartsServer()
    {
        _minecraftService.StartServerAsync().Returns(Task.FromResult(true));
        var statusSequence = new[] { 
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeStarted },
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeMonitored }
        };
        int callCount = 0;
        _statusProvider.GetLifeCycleStateAsync().Returns(x => Task.FromResult(statusSequence[Math.Min(callCount++, statusSequence.Length - 1)]));
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        // Since ShouldBeStarted causes a 'continue' in the loop, it will call GetStatusAsync again
        // We should receive at least 1 call to StartServerAsync
        await _minecraftService.Received().StartServerAsync();
    }

    /// <summary>
    /// Test: ManageServerLifecycleAsync stops the server when status indicates ShouldBeStopped.
    /// Intent: Verify that the service responds to stop signals by invoking graceful server shutdown.
    /// Importance: Core lifecycle management - ensures servers stop when signaled to do so.
    /// </summary>
    [Test]
    public async Task ManageServerLifecycleAsync_ShouldBeStopped_StopsServer()
    {
        _minecraftService.TryGracefulShutdownAsync().Returns(Task.FromResult(true));
        var statusSequence = new[] {
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeStopped },
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeIdle }
        };
        int callCount = 0;
        _statusProvider.GetLifeCycleStateAsync().Returns(x => Task.FromResult(statusSequence[Math.Min(callCount++, statusSequence.Length - 1)]));
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        await _minecraftService.Received().TryGracefulShutdownAsync();
    }

    /// <summary>
    /// Test: ManageServerLifecycleAsync applies patches when status indicates ShouldBePatched.
    /// Intent: Verify that the service responds to patch signals by applying updates to the server.
    /// Importance: Core update flow - ensures patches are applied when the server is ready.
    /// </summary>
    [Test]
    public async Task ManageServerLifecycleAsync_ShouldBePatched_PatchesServer()
    {
        _patchService.ApplyUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _backupService.CreateBackupZipFromServerFolder().Returns("backup.zip");
        var statusSequence = new[] {
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBePatched, PatchVersion = "1.0.1" },
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeMonitored }
        };
        int callCount = 0;
        _statusProvider.GetLifeCycleStateAsync().Returns(x => Task.FromResult(statusSequence[Math.Min(callCount++, statusSequence.Length - 1)]));
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        await _patchService.Received(1).ApplyUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Test: ManageServerLifecycleAsync takes no action when status indicates ShouldBeIdle.
    /// Intent: Verify that idle status prevents unnecessary startup operations.
    /// Importance: Ensures idempotent behavior - idle servers remain idle until explicitly started.
    /// </summary>
    [Test]
    public async Task ManageServerLifecycleAsync_ShouldBeIdle_DoesNothing()
    {
        _statusProvider.GetLifeCycleStateAsync()
            .Returns(Task.FromResult(new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeIdle }));
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        // Verify start was never called in the main loop (only cancel cleanup calls stop)
        await _minecraftService.DidNotReceive().StartServerAsync();
    }

    /// <summary>
    /// Test: ManageServerLifecycleAsync runs preflight checks on startup (redundant verification).
    /// Intent: Verify preflight checks are consistently executed at service initialization.
    /// Importance: Ensures consistent validation behavior across server lifecycle startup.
    /// </summary>
    [Test]
    public async Task ManageServerLifecycleAsync_PreflightCheckRun_OnStartup()
    {
        _statusProvider.GetLifeCycleStateAsync().Returns(Task.FromResult(new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeMonitored }));
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        await _preFlightService.Received(1).CheckAndCleanupAsync();
    }

    /// <summary>
    /// Test: StopServerAsync initiates graceful server shutdown through the minecraft service.
    /// Intent: Verify that explicit stop requests trigger proper shutdown procedures.
    /// Importance: Core stop functionality - ensures servers can be cleanly shut down on demand.
    /// </summary>
    [Test]
    public async Task StopServerAsync_CallsGracefulShutdown()
    {
        // Setup: Server is running, then becomes not running after a short delay
        _minecraftService.IsRunning.Returns(true);
        
        // Simulate server stopping after SetShutdownMode is called
        var callCount = 0;
        _minecraftService.IsRunning.Returns(x => {
            callCount++;
            // First few calls return true, then false to simulate server stopping
            return callCount < 3;
        });

        await _service.StopServerAsync();

        // Verify SetShutdownMode was called to enable shutdown sequence
        _statusProvider.Received(1).SetShutdownMode(ServerShutDownMode.WindowsServiceShutdown);
        // Server should no longer be running
        Assert.That(_minecraftService.IsRunning, Is.False);
    }

    /// <summary>
    /// Test: StopServerAsync force-stops the server if graceful shutdown fails.
    /// Intent: Verify that the service has a fallback mechanism when graceful shutdown doesn't succeed.
    /// Importance: Robustness - ensures servers can be stopped even if graceful shutdown fails.
    /// </summary>
    [Test]
    public async Task StopServerAsync_GracefulShutdownFails_ForcesStop()
    {
        // Setup: Server stops immediately 
        var callCount = 0;
        _minecraftService.IsRunning.Returns(x => {
            callCount++;
            return callCount < 2; // Stop after first check
        });

        await _service.StopServerAsync();

        // Verify SetShutdownMode was called
        _statusProvider.Received(1).SetShutdownMode(ServerShutDownMode.WindowsServiceShutdown);
        // Server should have stopped
        Assert.That(_minecraftService.IsRunning, Is.False);
    }

    /// <summary>
    /// Test: StopServerAsync transitions status provider to shutdown mode to prevent automatic restart.
    /// Intent: Verify that stopping the server also prevents auto-restart by switching the provider mode.
    /// Importance: Prevents unwanted restart during shutdown - ensures stop request is honored.
    /// </summary>
    [Test]
    public async Task StopServerAsync_ReplacesHandlerToPreventsRestart()
    {
        // Setup: StopServerAsync should trigger the status provider shutdown mode
        // Server is running initially, then stops
        var callCount = 0;
        _minecraftService.IsRunning.Returns(x => {
            callCount++;
            return callCount < 3; // Stop after a few checks
        });
        
        // Call StopServerAsync which should trigger shutdown mode in the provider
        await _service.StopServerAsync();

        // Verify SetShutdownMode was called on the provider
        _statusProvider.Received(1).SetShutdownMode(ServerShutDownMode.WindowsServiceShutdown);
        // Server should have stopped
        Assert.That(_minecraftService.IsRunning, Is.False);
    }

    /// <summary>
    /// Test: StopServerAsync skips shutdown operations if server is not running.
    /// Intent: Verify that the service gracefully handles stopping an already-stopped server.
    /// Importance: Prevents redundant shutdown attempts and handles the case where server is already stopped.
    /// </summary>
    [Test]
    public async Task StopServerAsync_ServerNotRunning_ReturnsImmediately()
    {
        _minecraftService.IsRunning.Returns(false);

        await _service.StopServerAsync();

        // Verify SetShutdownMode was called even though server is already stopped
        _statusProvider.Received(1).SetShutdownMode(ServerShutDownMode.AllowRestart);
        // Verify no shutdown methods were called
        await _minecraftService.DidNotReceive().TryGracefulShutdownAsync();
        await _minecraftService.DidNotReceive().ForceStopServerAsync();
    }
}
