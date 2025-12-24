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
        
        _statusFuncs = new ServerStatusFuncs
        {
            NormalStatusFunc = () => _statusService.GetStatusAsync(),
            ShutdownStatusFunc = new ShutdownStatusFunc().GetStatusAsync
        };
        
        _provider = new ServerStatusProvider(_statusFuncs);
    }

    [Test]
    public async Task GetStatusAsync_DefaultMode_CallsStatusService()
    {
        _statusService.GetStatusAsync().Returns(Task.FromResult(MineCraftServerStatus.ShouldBeMonitored));

        var status = await _provider.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));
        await _statusService.Received(1).GetStatusAsync();
    }

    [Test]
    public async Task GetStatusAsync_DefaultMode_RepeatedCalls_CallsServiceEachTime()
    {
        _statusService.GetStatusAsync()
            .Returns(
                Task.FromResult(MineCraftServerStatus.ShouldBeMonitored),
                Task.FromResult(MineCraftServerStatus.ShouldBeStarted),
                Task.FromResult(MineCraftServerStatus.ShouldBeStopped)
            );

        var status1 = await _provider.GetStatusAsync();
        var status2 = await _provider.GetStatusAsync();
        var status3 = await _provider.GetStatusAsync();

        Assert.That(status1, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));
        Assert.That(status2, Is.EqualTo(MineCraftServerStatus.ShouldBeStarted));
        Assert.That(status3, Is.EqualTo(MineCraftServerStatus.ShouldBeStopped));
        await _statusService.Received(3).GetStatusAsync();
    }

    [Test]
    public async Task SetShutdownMode_ReturnsShutdownSequence()
    {
        _provider.SetShutdownMode();

        // First call returns ShouldBeStopped
        var status1 = await _provider.GetStatusAsync();
        Assert.That(status1, Is.EqualTo(MineCraftServerStatus.ShouldBeStopped));

        // Second call returns ShouldBeIdle
        var status2 = await _provider.GetStatusAsync();
        Assert.That(status2, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));

        // Subsequent calls return ShouldBeIdle
        var status3 = await _provider.GetStatusAsync();
        Assert.That(status3, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));

        // StatusService should not be called during shutdown mode
        await _statusService.DidNotReceive().GetStatusAsync();
    }

    [Test]
    public async Task SetShutdownMode_DoesNotCallStatusService()
    {
        _provider.SetShutdownMode();

        await _provider.GetStatusAsync();
        await _provider.GetStatusAsync();
        await _provider.GetStatusAsync();

        // Verify status service was never called
        await _statusService.DidNotReceive().GetStatusAsync();
    }

    [Test]
    public async Task SetShutdownMode_AfterDefaultMode_SwitchesCorrectly()
    {
        // Start in default mode
        _statusService.GetStatusAsync().Returns(Task.FromResult(MineCraftServerStatus.ShouldBeMonitored));
        var status1 = await _provider.GetStatusAsync();
        Assert.That(status1, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));

        // Switch to shutdown mode
        _provider.SetShutdownMode();

        // Now it should use shutdown sequence, not status service
        var status2 = await _provider.GetStatusAsync();
        Assert.That(status2, Is.EqualTo(MineCraftServerStatus.ShouldBeStopped));

        var status3 = await _provider.GetStatusAsync();
        Assert.That(status3, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));

        // Status service should only be called once (from the first GetStatusAsync)
        await _statusService.Received(1).GetStatusAsync();
    }

    [Test]
    public void ServerStatusProvider_ThrowsIfStatusFuncsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ServerStatusProvider(null!));
    }
}
