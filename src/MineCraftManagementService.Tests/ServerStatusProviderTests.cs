using MineCraftManagementService.Enums;
using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class ServerStatusProviderTests
{
    private IServerStatusProvider _provider = null!;
    private IServerStatusService _statusService = null!;
    private ServerStatusHandlers _SrverStatusHandlers = null!;

    [SetUp]
    public void Setup()
    {
        _statusService = Substitute.For<IServerStatusService>();
        var autoShutdownTimeExceedeHandler = new AutoShutdownTimeExceededStatusHandler();

        _SrverStatusHandlers = new ServerStatusHandlers
        {
            NormalStatusHandler = () => _statusService.GetLifeCycleStateAsync(),
            WindowsServiceShutdownStatusHandler = new ShutdownStatusHandler().GetStatusAsync,
            AutoShutdownTimeExceededHandler = autoShutdownTimeExceedeHandler.GetStatusAsync
        };

        _provider = new ServerStatusProvider(_SrverStatusHandlers);
    }

    /// <summary>
    /// Test: Status service is called when provider is in default mode.
    /// Intent: Verify normal operation delegates to status service.
    /// Importance: Delegation - ensures primary logic is executed.
    /// </summary>
    [Test]
    public async Task Test_That_StatusService_Called_In_DefaultMode()
    {
        _statusService.GetLifeCycleStateAsync().Returns(Task.FromResult(new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeMonitored }));

        var status = await _provider.GetLifeCycleStateAsync();

        Assert.That(status.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));
        await _statusService.Received(1).GetLifeCycleStateAsync();
    }

    /// <summary>
    /// Test: Status service is called on each request in default mode (not cached).
    /// Intent: Verify fresh status is retrieved each time.
    /// Importance: Correctness - ensures status is current, not stale.
    /// </summary>
    [Test]
    public async Task Test_That_StatusService_CalledEachTime_NoCache_DefaultMode()
    {
        _statusService.GetLifeCycleStateAsync()
            .Returns(
                Task.FromResult(new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeMonitored }),
                Task.FromResult(new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeStarted }),
                Task.FromResult(new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeStopped })
            );

        var status1 = await _provider.GetLifeCycleStateAsync();
        var status2 = await _provider.GetLifeCycleStateAsync();
        var status3 = await _provider.GetLifeCycleStateAsync();

        Assert.That(status1.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));
        Assert.That(status2.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeStarted));
        Assert.That(status3.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeStopped));
        await _statusService.Received(3).GetLifeCycleStateAsync();
    }

    /// <summary>
    /// Test: SetShutdownMode switches to a predetermined shutdown sequence (ShouldBeStopped then ShouldBeIdle).
    /// Intent: Verify that shutdown mode returns the correct sequence of statuses for a clean server shutdown flow.
    /// Importance: Critical for graceful shutdown - ensures proper sequencing of stop then idle states.
    /// </summary>
    [Test]
    [TestCase(ServerShutDownMode.WindowsServiceShutdown)]
    public async Task Test_That_ShutdownSequence_Returned_In_ShutdownMode(ServerShutDownMode shutDownMode)
    {
        _provider.SetShutdownMode(shutDownMode);

        // First call returns ShouldBeStopped
        var status1 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status1.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeStopped));

        // Second call returns ShouldBeIdle
        var status2 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status2.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));

        // Subsequent calls return ShouldBeIdle
        var status3 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status3.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));

        // StatusService should not be called during shutdown mode
        await _statusService.DidNotReceive().GetLifeCycleStateAsync();
    }

    /// <summary>
    /// Test: In shutdown mode, status service is not called - shutdown sequence is used exclusively.
    /// Intent: Verify that shutdown mode completely bypasses the status service during shutdown.
    /// Importance: Prevents interference from status checks during shutdown and ensures deterministic shutdown sequence.
    /// </summary>
    [Test]
    [TestCase(ServerShutDownMode.DenyRestart)]
    public async Task Test_That_StatusService_Bypassed_In_ShutdownMode(ServerShutDownMode mode)
    {
        _provider.SetShutdownMode(mode);

        var lifecycleState = await _provider.GetLifeCycleStateAsync();
        Assert.That(lifecycleState.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));
        // Verify status service was never called
        await _statusService.DidNotReceive().GetLifeCycleStateAsync();
    }

    /// <summary>
    /// Test: Provider correctly transitions from default mode to shutdown sequence mode.
    /// Intent: Verify that the provider can switch from normal operation to shutdown mode and use the shutdown sequence thereafter.
    /// Importance: Ensures mode switching works correctly - normal operation seamlessly transitions to shutdown handling.
    /// </summary>
    [Test]
    public async Task Test_That_ModeSwitch_Works_From_DefaultToShutdown()
    {
        // Start in default mode
        _statusService.GetLifeCycleStateAsync().Returns(Task.FromResult(new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeMonitored }));
        var status1 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status1.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));

        // Switch to shutdown mode
        _provider.SetShutdownMode(ServerShutDownMode.WindowsServiceShutdown);

        // Now it should use shutdown sequence, not status service
        var status2 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status2.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeStopped));

        var status3 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status3.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));

        // Status service should only be called once (from the first GetStatusAsync)
        await _statusService.Received(1).GetLifeCycleStateAsync();
    }

    /// <summary>
    /// Test: Once auto-shutdown time exceeded, ShouldBeStopped always returns to prevent restart.
    /// Intent: Verify that auto-shutdown cannot be escaped once triggered.
    /// Importance: Safety - ensures auto-shutdown always blocks restart.
    /// </summary>
    [Test]
    public async Task Test_That_Once_AutoShutDownTimeExceeded_AlwaysReturnsShouldBeIdle()
    {
        _provider.SetShutdownMode(ServerShutDownMode.DenyRestart);

        // First call returns ShouldBeIdle
        var status1 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status1.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));

        // Second call returns ShouldBeIdle
        var status2 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status2.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));

        // Subsequent calls always return ShouldBeIdle
        var status3 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status3.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));

        // StatusService should not be called during shutdown mode
        await _statusService.DidNotReceive().GetLifeCycleStateAsync();
    }

    /// <summary>
    /// Test: ServerStatusProvider validates required dependencies and throws when constructed with null.
    /// Intent: Verify that the provider fails fast with missing configuration.
    /// Importance: Prevents silent failures - catches configuration errors immediately.
    /// </summary>
    [Test]
    public void Test_That_Exception_Thrown_When_Constructed_WithNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ServerStatusProvider(null!));
    }
}
