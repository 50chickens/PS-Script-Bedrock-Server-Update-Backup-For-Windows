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
    private IServerStatusService _statusService = null!;
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
        _statusService = Substitute.For<IServerStatusService>();
        _statusProvider = Substitute.For<IServerStatusProvider>();
        _patchService = Substitute.For<IMinecraftServerPatchService>();
        _updateService = Substitute.For<IMineCraftUpdateService>();
        _backupService = Substitute.For<IMineCraftBackupService>();
        _autoStartService = Substitute.For<IServerAutoStartService>();
        _options = TestUtils.CreateOptions();

        _service = new ServerLifecycleService(_log, _minecraftService, _preFlightService, _statusService, _statusProvider, _patchService, _updateService, _backupService, _autoStartService, _options);
    }

    [Test]
    public async Task ManageServerLifecycleAsync_Preflight_RanOnStartup()
    {
        _statusProvider.GetStatusAsync().Returns(Task.FromResult(MineCraftServerStatus.ShouldBeMonitored));
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        await _preFlightService.Received(1).CheckAndCleanupAsync();
    }

    [Test]
    public async Task ManageServerLifecycleAsync_AutoStartEnabled_StartsServer()
    {
        _autoStartService.ApplyAutoStartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _statusProvider.GetStatusAsync().Returns(Task.FromResult(MineCraftServerStatus.ShouldBeMonitored));
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        await _autoStartService.Received(1).ApplyAutoStartAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ManageServerLifecycleAsync_ShouldBeStarted_StartsServer()
    {
        _minecraftService.StartServerAsync().Returns(Task.FromResult(true));
        var statusSequence = new[] { MineCraftServerStatus.ShouldBeStarted, MineCraftServerStatus.ShouldBeMonitored };
        int callCount = 0;
        _statusProvider.GetStatusAsync().Returns(x => Task.FromResult(statusSequence[Math.Min(callCount++, statusSequence.Length - 1)]));
        
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

    [Test]
    public async Task ManageServerLifecycleAsync_ShouldBeStopped_StopsServer()
    {
        _minecraftService.TryGracefulShutdownAsync().Returns(Task.FromResult(true));
        var statusSequence = new[] { MineCraftServerStatus.ShouldBeStopped, MineCraftServerStatus.ShouldBeIdle };
        int callCount = 0;
        _statusProvider.GetStatusAsync().Returns(x => Task.FromResult(statusSequence[Math.Min(callCount++, statusSequence.Length - 1)]));
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        await _minecraftService.Received().TryGracefulShutdownAsync();
    }

    [Test]
    public async Task ManageServerLifecycleAsync_ShouldBePatched_PatchesServer()
    {
        _patchService.ApplyUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _backupService.CreateBackupZipFromServerFolder().Returns("backup.zip");
        var statusSequence = new[] { MineCraftServerStatus.ShouldBePatched, MineCraftServerStatus.ShouldBeMonitored };
        int callCount = 0;
        _statusProvider.GetStatusAsync().Returns(x => Task.FromResult(statusSequence[Math.Min(callCount++, statusSequence.Length - 1)]));
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        await _patchService.Received(1).ApplyUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ManageServerLifecycleAsync_ShouldBeIdle_DoesNothing()
    {
        _statusProvider.GetStatusAsync()
            .Returns(Task.FromResult(MineCraftServerStatus.ShouldBeIdle));
        
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

    [Test]
    public async Task ManageServerLifecycleAsync_PreflightCheckRun_OnStartup()
    {
        _statusProvider.GetStatusAsync().Returns(Task.FromResult(MineCraftServerStatus.ShouldBeMonitored));
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        try
        {
            await _service.ManageServerLifecycleAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        await _preFlightService.Received(1).CheckAndCleanupAsync();
    }

    [Test]
    public async Task StopServerAsync_CallsGracefulShutdown()
    {
        _minecraftService.TryGracefulShutdownAsync().Returns(Task.FromResult(true));

        await _service.StopServerAsync();

        await _minecraftService.Received(1).TryGracefulShutdownAsync();
    }

    [Test]
    public async Task StopServerAsync_GracefulShutdownFails_ForcesStop()
    {
        _minecraftService.TryGracefulShutdownAsync().Returns(Task.FromResult(false));
        _minecraftService.ForceStopServerAsync().Returns(Task.FromResult(true));

        await _service.StopServerAsync();

        await _minecraftService.Received(1).TryGracefulShutdownAsync();
        await _minecraftService.Received(1).ForceStopServerAsync();
    }

    [Test]
    public async Task StopServerAsync_ReplacesFuncToPreventsRestart()
    {
        // Setup: StopServerAsync should trigger the status provider shutdown mode
        _minecraftService.TryGracefulShutdownAsync().Returns(Task.FromResult(true));
        
        // Initially return ShouldBeStarted
        _statusProvider.GetStatusAsync().Returns(Task.FromResult(MineCraftServerStatus.ShouldBeStarted));

        // Call StopServerAsync which should trigger shutdown mode in the provider
        await _service.StopServerAsync();

        // Verify TryGracefulShutdownAsync was called
        await _minecraftService.Received(1).TryGracefulShutdownAsync();
        // Verify SetShutdownMode was called on the provider
        _statusProvider.Received(1).SetShutdownMode();
    }
}
