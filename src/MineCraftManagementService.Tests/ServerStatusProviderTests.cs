using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class ServerStatusProviderTests
{
    private IServerStatusProvider _provider = null!;
    private IServerStatusService _statusService = null!;
    private ServerStatusFuncs _statusFuncs = null!;

    [SetUp]
    public void Setup()
    {
        _statusService = Substitute.For<IServerStatusService>();
        var autoShutdownIdleFunc = new AutoShutdownIdleFunc();
        
        _statusFuncs = new ServerStatusFuncs
        {
            NormalStatusFunc = () => _statusService.GetLifeCycleStateAsync(),
            ShutdownStatusFunc = new ShutdownStatusFunc().GetStatusAsync,
            AutoShutdownIdleFunc = () => autoShutdownIdleFunc.GetStatusAsync()
        };
        
        _provider = new ServerStatusProvider(_statusFuncs);
    }

    /// <summary>
    /// Test: GetStatusAsync in default mode delegates to and returns the underlying status service result.
    /// Intent: Verify that the provider correctly forwards calls to the status service in normal operation.
    /// Importance: Core delegation pattern - ensures status queries are properly routed to the service layer.
    /// </summary>
    [Test]
    public async Task GetStatusAsync_DefaultMode_CallsStatusService()
    {
        _statusService.GetLifeCycleStateAsync().Returns(Task.FromResult(new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeMonitored }));

        var status = await _provider.GetLifeCycleStateAsync();

        Assert.That(status.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));
        await _statusService.Received(1).GetLifeCycleStateAsync();
    }

    /// <summary>
    /// Test: Repeated GetStatusAsync calls in default mode each delegate to status service independently.
    /// Intent: Verify that the provider doesn't cache results and freshly queries the service on each call.
    /// Importance: Ensures real-time status updates - changes in server state are detected on every query.
    /// </summary>
    [Test]
    public async Task GetStatusAsync_DefaultMode_RepeatedCalls_CallsServiceEachTime()
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
    public async Task SetShutdownMode_ReturnsShutdownSequence()
    {
        _provider.SetShutdownMode();

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
    /// Test: In shutdown mode, the status service is not called - shutdown sequence is used exclusively.
    /// Intent: Verify that shutdown mode completely bypasses the status service during shutdown.
    /// Importance: Prevents interference from status checks during shutdown and ensures deterministic shutdown sequence.
    /// </summary>
    [Test]
    public async Task SetShutdownMode_DoesNotCallStatusService()
    {
        _provider.SetShutdownMode();

        await _provider.GetLifeCycleStateAsync();
        await _provider.GetLifeCycleStateAsync();
        await _provider.GetLifeCycleStateAsync();

        // Verify status service was never called
        await _statusService.DidNotReceive().GetLifeCycleStateAsync();
    }

    /// <summary>
    /// Test: SetShutdownMode correctly transitions from default mode to shutdown sequence mode.
    /// Intent: Verify that the provider can switch from normal operation to shutdown mode and use the shutdown sequence thereafter.
    /// Importance: Ensures mode switching works correctly - normal operation seamlessly transitions to shutdown handling.
    /// </summary>
    [Test]
    public async Task SetShutdownMode_AfterDefaultMode_SwitchesCorrectly()
    {
        // Start in default mode
        _statusService.GetLifeCycleStateAsync().Returns(Task.FromResult(new MineCraftServerLifecycleStatus { LifecycleStatus = MineCraftServerStatus.ShouldBeMonitored }));
        var status1 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status1.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));

        // Switch to shutdown mode
        _provider.SetShutdownMode();

        // Now it should use shutdown sequence, not status service
        var status2 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status2.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeStopped));

        var status3 = await _provider.GetLifeCycleStateAsync();
        Assert.That(status3.LifecycleStatus, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));

        // Status service should only be called once (from the first GetStatusAsync)
        await _statusService.Received(1).GetLifeCycleStateAsync();
    }

    /// <summary>
    /// Test: ServerStatusProvider throws ArgumentNullException when constructed with null status functions.
    /// Intent: Verify that the provider validates required dependencies and fails fast with missing configuration.
    /// Importance: Prevents silent failures - ensures constructor validates critical dependencies.
    /// </summary>
    [Test]
    public void ServerStatusProvider_ThrowsIfStatusFuncsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ServerStatusProvider(null!));
    }
}
