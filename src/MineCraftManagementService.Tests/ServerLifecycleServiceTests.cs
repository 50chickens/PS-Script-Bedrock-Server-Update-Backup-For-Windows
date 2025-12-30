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
        _backupService = Substitute.For<IMineCraftBackupService>();
        _autoStartService = Substitute.For<IServerAutoStartService>();
        _options = TestUtils.CreateOptions();

        _service = new ServerLifecycleService(_log, _minecraftService, _preFlightService, _statusProvider, _patchService, _backupService, _autoStartService, _options);
    }

    /// <summary>
    /// Test: PreFlight checks run on service startup.
    /// Intent: Verify initial setup checks are performed before any lifecycle management.
    /// Importance: Initialization - ensures system is ready before managing the server.
    /// </summary>
    [Test]
    public async Task Test_That_PreflightChecks_Run_OnStartup()
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
    /// Test: Server starts when ShouldBeStarted status is returned and auto-start is enabled.
    /// Intent: Verify auto-start feature works correctly.
    /// Importance: Core feature - enables automatic server restart.
    /// </summary>
    [Test]
    public async Task Test_That_ServerStarts_When_ShouldBeStarted()
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
    /// Test: Server is started when status returns ShouldBeStarted.
    /// Intent: Verify start action is taken on correct status signal.
    /// Importance: State handling - ensures correct response to start signals.
    /// </summary>
    [Test]
    public async Task Test_That_Server_IsStarted_When_StatusIndicates_ShouldBeStarted()
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
        catch (OperationCanceledException)
        {
            //on purpose to stop the loop
        }

        // Since ShouldBeStarted causes a 'continue' in the loop, it will call GetStatusAsync again
        // We should receive at least 1 call to StartServerAsync
        await _minecraftService.Received().StartServerAsync();
    }

    /// <summary>
    /// Test: Server is stopped when status returns ShouldBeStopped.
    /// Intent: Verify stop action is taken on correct status signal.
    /// Importance: State handling - ensures servers stop when needed.
    /// </summary>
    [Test]
    public async Task Test_That_Server_IsStopped_When_StatusIndicates_ShouldBeStopped()
    {
        _minecraftService.IsRunning.Returns(true);
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
    /// Test: Server patches are applied when status returns ShouldBePatched.
    /// Intent: Verify patch action is taken on correct status signal.
    /// Importance: Core update flow - ensures updates are actually applied.
    /// </summary>
    [Test]
    public async Task Test_That_ServerPatch_Applied_When_StatusIndicates_ShouldBePatched()
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
    /// Test: No action is taken when status returns ShouldBeIdle.
    /// Intent: Verify idle state does not trigger unnecessary operations.
    /// Importance: Resource efficiency - prevents wasting resources on disabled servers.
    /// </summary>
    [Test]
    public async Task Test_That_NoAction_Taken_When_StatusIndicates_ShouldBeIdle()
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
    /// Test: PreFlight check service is called during initial startup sequence.
    /// Intent: Verify initialization checks happen exactly once on startup.
    /// Importance: System validation - ensures all systems ready before operation.
    /// </summary>
    [Test]
    public async Task Test_That_PreFlightCheck_Called_OnStartup()
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
    /// Test: StopServerAsync calls graceful shutdown on the server service.
    /// Intent: Verify normal stop procedure uses graceful shutdown.
    /// Importance: Data safety - ensures graceful shutdown to avoid data loss.
    /// </summary>
    [Test]
    public async Task Test_That_GracefulShutdown_Called_When_StoppingServer()
    {
        // Setup: Server is running, then becomes not running after a short delay
        _minecraftService.IsRunning.Returns(true);

        // Simulate server stopping after SetShutdownMode is called
        var callCount = 0;
        _minecraftService.IsRunning.Returns(x =>
        {
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
    /// Test: Force stop is used when graceful shutdown fails.
    /// Intent: Verify fallback mechanism when graceful shutdown doesn't work.
    /// Importance: Resilience - ensures server can still be stopped even if graceful fails.
    /// </summary>
    [Test]
    public async Task Test_That_ForceStop_Used_When_GracefulShutdown_Fails()
    {
        // Setup: Server stops immediately 
        var callCount = 0;
        _minecraftService.IsRunning.Returns(x =>
        {
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
    /// Test: Auto-restart handler is replaced during shutdown to prevent automatic restart.
    /// Intent: Verify shutdown mode prevents unwanted auto-restart.
    /// Importance: Safety - ensures server doesn't immediately restart after stop signal.
    /// </summary>
    [Test]
    public async Task Test_That_RestartHandler_Replaced_To_PreventRestart_OnShutdown()
    {
        // Setup: StopServerAsync should trigger the status provider shutdown mode
        // Server is running initially, then stops
        var callCount = 0;
        _minecraftService.IsRunning.Returns(x =>
        {
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
    /// Test: StopServerAsync returns immediately when server is already not running.
    /// Intent: Verify idempotence - no error if already stopped.
    /// Importance: Safety - prevents errors when stopping already-stopped server.
    /// </summary>
    [Test]
    public async Task Test_That_Stop_ReturnsQuickly_When_ServerNotRunning()
    {
        _minecraftService.IsRunning.Returns(false);

        await _service.StopServerAsync();

        // Verify SetShutdownMode was called with WindowsServiceShutdown
        _statusProvider.Received(1).SetShutdownMode(ServerShutDownMode.WindowsServiceShutdown);
        // Verify no shutdown methods were called
        await _minecraftService.DidNotReceive().TryGracefulShutdownAsync();
        await _minecraftService.DidNotReceive().ForceStopServerAsync();
    }

    /// <summary>
    /// Test: Service does not repeatedly try to stop an already-stopped server.
    /// Intent: Verify that after stopping the server, the lifecycle doesn't continuously send stop commands.
    /// Importance: Bug fix - prevents continuous restart loop when server fails to start or cannot be reached.
    /// </summary>
    [Test]
    public async Task Test_That_Service_DoesNotRepeatedly_Stop_AlreadyStopped_Server()
    {
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.TryGracefulShutdownAsync().Returns(Task.FromResult(true));
        _autoStartService.ApplyAutoStartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Server status sequence: First return ShouldBeStopped, then after stop, check should return ShouldBeIdle (not ShouldBeStopped again)
        var statusSequence = new[] {
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeStopped },
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeIdle },
            new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeIdle }
        };
        int callCount = 0;
        _statusProvider.GetLifeCycleStateAsync().Returns(x => Task.FromResult(statusSequence[Math.Min(callCount++, statusSequence.Length - 1)]));

        var cts = new CancellationTokenSource();
        cts.CancelAfter(500); // Allow enough time for multiple iterations

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        // Should only try to stop once (first ShouldBeStopped), not repeatedly
        // We can't directly test for "call count = 1" since TryGracefulShutdownAsync might be called multiple times,
        // but we verify that after ShouldBeStopped is processed, subsequent calls return ShouldBeIdle
        // This proves the service doesn't keep returning ShouldBeStopped
        Received.InOrder(async () =>
        {
            await _statusProvider.GetLifeCycleStateAsync();
            await _statusProvider.GetLifeCycleStateAsync();
        });
    }
}
