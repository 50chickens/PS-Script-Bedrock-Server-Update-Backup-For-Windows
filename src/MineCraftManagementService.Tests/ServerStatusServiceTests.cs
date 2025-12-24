using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class ServerStatusServiceTests
{
    private IServerStatusService _service = null!;
    private ILog<ServerStatusService> _log = null!;
    private IMineCraftServerService _minecraftService = null!;
    private IMineCraftUpdateService _updateService = null!;
    private MineCraftServerOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<ServerStatusService>();
        _minecraftService = Substitute.For<IMineCraftServerService>();
        _updateService = Substitute.For<IMineCraftUpdateService>();
        _options = TestUtils.CreateOptions();
        
        _service = new ServerStatusService(_log, _minecraftService, _options, _updateService);
    }

    [Test]
    public async Task GetStatusAsync_ServerNotRunning_NoAutoStart_ReturnsIdle()
    {
        var options = TestUtils.CreateOptions();
        options.EnableAutoStart = false;
        options.AutoShutdownAfterSeconds = 0;
        _service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.MinValue);

        var status = await _service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));
    }

    [Test]
    public async Task GetStatusAsync_ServerNotRunning_AutoStartEnabled_ReturnsShouldBeStarted()
    {
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.MinValue);
        _options.EnableAutoStart = true;
        _options.AutoShutdownAfterSeconds = 0;

        var status = await _service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeStarted));
    }

    [Test]
    public async Task GetStatusAsync_ServerRunning_UpToDate_ReturnsMonitored()
    {
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((false, "", "")));
        _options.CheckForUpdates = true;
        _options.UpdateCheckIntervalSeconds = 1;

        var status = await _service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));
    }

    [Test]
    public async Task GetStatusAsync_UpdateAvailable_ServerRunning_ReturnsShouldBeStopped()
    {
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));
        _options.CheckForUpdates = true;
        _options.UpdateCheckIntervalSeconds = 1;

        var status = await _service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeStopped));
    }

    [Test]
    public async Task GetStatusAsync_UpdateAvailable_ServerStopped_ReturnsShouldBePatched()
    {
        _minecraftService.IsRunning.Returns(false);
        // Server was stopped after running for longer than minimum uptime
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-120));
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));
        _options.CheckForUpdates = true;
        _options.UpdateCheckIntervalSeconds = 1;
        _options.MinimumServerUptimeForUpdateSeconds = 60;

        var status = await _service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBePatched));
    }

    [Test]
    public async Task GetStatusAsync_AutoShutdownExceeded_ServerRunning_ReturnsShouldBeStopped()
    {
        var options = TestUtils.CreateOptions();
        options.AutoShutdownAfterSeconds = 50;
        _service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-100));

        var status = await _service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeStopped));
    }

    [Test]
    public async Task GetStatusAsync_AutoShutdownExceeded_ServerStopped_AutoStartDisabled_ReturnsIdle()
    {
        var options = TestUtils.CreateOptions();
        options.AutoShutdownAfterSeconds = 50;
        options.EnableAutoStart = false;
        _service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-100));

        var status = await _service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));
    }

    [Test]
    public async Task GetStatusAsync_CheckForUpdatesDisabled_ServerRunning_ReturnsMonitored()
    {
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        _options.CheckForUpdates = false;

        var status = await _service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));
    }

    [Test]
    public async Task GetStatusAsync_AutoShutdownExceeded_ServerStopped_AutoStartEnabled_ReturnsShouldBeStarted()
    {
        var options = TestUtils.CreateOptions();
        options.AutoShutdownAfterSeconds = 50;
        options.EnableAutoStart = true;
        _service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-100));

        var status = await _service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeStarted));
    }

    #region Internal Method Tests

    [Test]
    public void ShouldBeStopped_AutoShutdownExceeded_ServerRunning_ReturnsTrue()
    {
        var options = TestUtils.CreateOptions();
        options.AutoShutdownAfterSeconds = 50;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-100));

        var result = service.ShouldBeStopped();

        Assert.That(result, Is.True);
    }

    [Test]
    public void ShouldBeStopped_AutoShutdownNotExceeded_ServerRunning_ReturnsFalse()
    {
        var options = TestUtils.CreateOptions();
        options.AutoShutdownAfterSeconds = 600; // 10 minutes
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-100));

        var result = service.ShouldBeStopped();

        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldBeStopped_AutoShutdownExceeded_ServerStopped_ReturnsFalse()
    {
        var options = TestUtils.CreateOptions();
        options.AutoShutdownAfterSeconds = 50;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-100));

        var result = service.ShouldBeStopped();

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ShouldBeStoppedForUpdate_UpdateAvailable_ServerRunning_ReturnsTrue()
    {
        var service = (ServerStatusService)_service;
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _options.CheckForUpdates = true;
        _options.UpdateCheckIntervalSeconds = 1;
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var result = await service.ShouldBeStoppedForUpdate();

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ShouldBeStoppedForUpdate_NoUpdateAvailable_ServerRunning_ReturnsFalse()
    {
        var service = (ServerStatusService)_service;
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _options.CheckForUpdates = true;
        _options.UpdateCheckIntervalSeconds = 1;
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((false, "", "")));

        var result = await service.ShouldBeStoppedForUpdate();

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ShouldBeStoppedForUpdate_ServerStopped_ReturnsFalse()
    {
        var service = (ServerStatusService)_service;
        _minecraftService.IsRunning.Returns(false);
        _options.CheckForUpdates = true;
        _options.UpdateCheckIntervalSeconds = 1;

        var result = await service.ShouldBeStoppedForUpdate();

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ShouldBeStoppedForUpdate_CheckForUpdatesDisabled_ReturnsFalse()
    {
        var service = (ServerStatusService)_service;
        _minecraftService.IsRunning.Returns(true);
        _options.CheckForUpdates = false;

        var result = await service.ShouldBeStoppedForUpdate();

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ShouldBePatched_UpdateAvailable_ServerStopped_ReturnsTrue()
    {
        var service = (ServerStatusService)_service;
        _minecraftService.IsRunning.Returns(false);
        // Server was stopped after running for longer than minimum uptime (default 60 seconds)
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-120));
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _options.CheckForUpdates = true;
        _options.UpdateCheckIntervalSeconds = 1;
        _options.MinimumServerUptimeForUpdateSeconds = 60;
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var result = await service.ShouldBePatched();

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ShouldBePatched_NoUpdateAvailable_ServerStopped_ReturnsFalse()
    {
        var service = (ServerStatusService)_service;
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _options.CheckForUpdates = true;
        _options.UpdateCheckIntervalSeconds = 1;
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((false, "", "")));

        var result = await service.ShouldBePatched();

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ShouldBePatched_ServerRunning_ReturnsFalse()
    {
        var service = (ServerStatusService)_service;
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _options.CheckForUpdates = true;
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var result = await service.ShouldBePatched();

        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldBeStartedOrIdle_AutoStartEnabled_ReturnsShouldBeStarted()
    {
        var options = TestUtils.CreateOptions();
        options.EnableAutoStart = true;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);

        var result = service.ShouldBeStartedOrIdle();

        Assert.That(result, Is.EqualTo(MineCraftServerStatus.ShouldBeStarted));
    }

    [Test]
    public void ShouldBeStartedOrIdle_AutoStartDisabled_ReturnsShouldBeIdle()
    {
        var options = TestUtils.CreateOptions();
        options.EnableAutoStart = false;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);

        var result = service.ShouldBeStartedOrIdle();

        Assert.That(result, Is.EqualTo(MineCraftServerStatus.ShouldBeIdle));
    }

    #endregion

    #region Complex Scenario Tests

    [Test]
    public async Task GetStatusAsync_MultipleConditionsCalls_CorrectPriority()
    {
        // Test that ShouldBeStopped is checked before ShouldBeStoppedForUpdate
        var options = TestUtils.CreateOptions();
        options.AutoShutdownAfterSeconds = 50;
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 1;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-100));
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        // Both conditions are true, but ShouldBeStopped should trigger first
        var status = await service.GetStatusAsync();

        // Should return ShouldBeStopped due to auto-shutdown, not ShouldBeStoppedForUpdate
        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeStopped));
    }

    [Test]
    public async Task GetStatusAsync_ShouldBeStoppedForUpdate_CheckedBeforeShouldBePatched()
    {
        // Test that update check for running server is done before patch check
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 1;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var status = await service.GetStatusAsync();

        // Should return ShouldBeStopped (for running server with update)
        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeStopped));
    }

    [Test]
    public async Task GetStatusAsync_UpdateCheckIntervalNotExceeded_NoUpdateCheck()
    {
        // Create a service with a long update check interval
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 3600; // 1 hour
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        _minecraftService.CurrentVersion.Returns("1.0.0");

        // First call should trigger the update check
        await service.GetStatusAsync();
        await _updateService.Received(1).NewVersionIsAvailable(Arg.Any<string>());

        // Reset the mock to verify second call doesn't check
        _updateService.ClearReceivedCalls();

        // Second call should NOT trigger another update check (interval not exceeded)
        await service.GetStatusAsync();
        await _updateService.DidNotReceive().NewVersionIsAvailable(Arg.Any<string>());
    }

    [Test]
    public async Task GetStatusAsync_UpdateCheckIntervalExceeded_UpdateCheckRuns()
    {
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 1; // Very short interval
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((false, "", "")));

        // First call checks for updates
        await service.GetStatusAsync();
        await _updateService.Received(1).NewVersionIsAvailable(Arg.Any<string>());

        _updateService.ClearReceivedCalls();

        // Wait for interval to pass and make another call
        await Task.Delay(1500); // 1.5 seconds
        await service.GetStatusAsync();
        
        // Second call should also check for updates (interval exceeded)
        await _updateService.Received(1).NewVersionIsAvailable(Arg.Any<string>());
    }

    [Test]
    public async Task GetStatusAsync_ServerRunning_NoUpdates_ChecksMonitoringStatus()
    {
        // This test verifies that when server is running and up-to-date,
        // it returns ShouldBeMonitored (not ShouldBeStarted or ShouldBeIdle)
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.AutoShutdownAfterSeconds = 0; // Disable auto-shutdown
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((false, "", "")));

        var status = await service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));
    }

    [Test]
    public async Task ShouldBeStoppedForUpdate_UpdateCheckIntervalNotExceeded_NoCheck()
    {
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 3600; // 1 hour
        var service = (ServerStatusService)new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-120)); // Started 2 minutes ago (exceeds 60 second minimum)
        _minecraftService.CurrentVersion.Returns("1.0.0");

        // First call triggers update check
        await service.ShouldBeStoppedForUpdate();
        await _updateService.Received(1).NewVersionIsAvailable(Arg.Any<string>());

        _updateService.ClearReceivedCalls();

        // Second call should NOT trigger another check (interval not exceeded)
        await service.ShouldBeStoppedForUpdate();
        await _updateService.DidNotReceive().NewVersionIsAvailable(Arg.Any<string>());
    }

    [Test]
    public async Task ShouldBePatched_UpdateCheckIntervalNotExceeded_NoCheck()
    {
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 3600; // 1 hour
        var service = (ServerStatusService)new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.CurrentVersion.Returns("1.0.0");

        // First call triggers update check
        await service.ShouldBePatched();
        await _updateService.Received(1).NewVersionIsAvailable(Arg.Any<string>());

        _updateService.ClearReceivedCalls();

        // Second call should NOT trigger another check (interval not exceeded)
        await service.ShouldBePatched();
        await _updateService.DidNotReceive().NewVersionIsAvailable(Arg.Any<string>());
    }

    [Test]
    public void ShouldBeStopped_AutoShutdownZero_ReturnsFalse()
    {
        var options = TestUtils.CreateOptions();
        options.AutoShutdownAfterSeconds = 0; // Disabled
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-1000));

        var result = service.ShouldBeStopped();

        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldBeStopped_AutoShutdownNegative_ReturnsFalse()
    {
        var options = TestUtils.CreateOptions();
        options.AutoShutdownAfterSeconds = -1; // Invalid but should handle gracefully
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-1000));

        var result = service.ShouldBeStopped();

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ShouldBePatched_CheckForUpdatesDisabled_ReturnsFalse()
    {
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = false;
        var service = (ServerStatusService)new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.MinValue);
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var result = await service.ShouldBePatched();

        Assert.That(result, Is.False);
        // Verify update service was never called
        await _updateService.DidNotReceive().NewVersionIsAvailable(Arg.Any<string>());
    }

    [Test]
    public async Task GetStatusAsync_CheckForUpdatesDisabled_SkipsUpdateChecks()
    {
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = false;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now);

        var status = await service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));
        // Verify update service was never called when CheckForUpdates is disabled
        await _updateService.DidNotReceive().NewVersionIsAvailable(Arg.Any<string>());
    }

    [Test]
    public void ShouldBeStopped_ServerStartTimeMinValue_TriggersStopped()
    {
        // DateTime.MinValue is a very old time, so the elapsed time will be huge
        // and will exceed any auto-shutdown threshold, returning True
        var options = TestUtils.CreateOptions();
        options.AutoShutdownAfterSeconds = 50;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.MinValue); // Very old time

        var result = service.ShouldBeStopped();

        // Since DateTime.MinValue means virtually infinite elapsed time,
        // it will exceed the auto-shutdown threshold
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task GetStatusAsync_ServerRunning_AutoShutdownNotExceeded_ReturnsMonitored()
    {
        var options = TestUtils.CreateOptions();
        options.AutoShutdownAfterSeconds = 600; // 10 minutes
        options.CheckForUpdates = true;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-100)); // 100 seconds elapsed
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((false, "", "")));

        var status = await service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBeMonitored));
    }

    [Test]
    public async Task ShouldBeStoppedForUpdate_UpdateAvailableButCheckDisabled_ReturnsFalse()
    {
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = false;
        var service = (ServerStatusService)new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(true);
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var result = await service.ShouldBeStoppedForUpdate();

        Assert.That(result, Is.False);
        // Verify update service was never called
        await _updateService.DidNotReceive().NewVersionIsAvailable(Arg.Any<string>());
    }

    #endregion

    #region Minimum Uptime Tests

    [Test]
    public void HasMinimumIdleTime_ServerNeverStarted_ReturnsFalse()
    {
        var service = (ServerStatusService)_service;
        _minecraftService.ServerStartTime.Returns(DateTime.MinValue);

        var result = service.HasMinimumIdleTime();

        Assert.That(result, Is.False);
    }

    [Test]
    public void HasMinimumIdleTime_MinimumTimeExceeded_ReturnsTrue()
    {
        var options = TestUtils.CreateOptions();
        options.MinimumServerUptimeForUpdateSeconds = 60;
        var service = (ServerStatusService)new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        // Server started 120 seconds ago (exceeds 60 second minimum)
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-120));

        var result = service.HasMinimumIdleTime();

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasMinimumIdleTime_MinimumTimeNotExceeded_ReturnsFalse()
    {
        var options = TestUtils.CreateOptions();
        options.MinimumServerUptimeForUpdateSeconds = 60;
        var service = (ServerStatusService)new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        // Server started only 30 seconds ago (less than 60 second minimum)
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-30));

        var result = service.HasMinimumIdleTime();

        Assert.That(result, Is.False);
    }

    [Test]
    public void HasMinimumIdleTime_ExactlyMinimumTime_ReturnsTrue()
    {
        var options = TestUtils.CreateOptions();
        options.MinimumServerUptimeForUpdateSeconds = 60;
        var service = (ServerStatusService)new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        // Server started exactly 60 seconds ago
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-60));

        var result = service.HasMinimumIdleTime();

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasMinimumIdleTime_MinimumTimeZero_ReturnsTrue()
    {
        var options = TestUtils.CreateOptions();
        options.MinimumServerUptimeForUpdateSeconds = 0;
        var service = (ServerStatusService)new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-1));

        var result = service.HasMinimumIdleTime();

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ShouldBePatched_UpdateAvailable_MinimumTimeExceeded_ReturnsTrue()
    {
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 1;
        options.MinimumServerUptimeForUpdateSeconds = 60;
        var service = (ServerStatusService)new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-120)); // 2 minutes uptime
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var result = await service.ShouldBePatched();

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ShouldBePatched_UpdateAvailable_MinimumTimeNotExceeded_ReturnsTrue()
    {
        // Minimum uptime check is done in ShouldBeStoppedForUpdate before stopping the server.
        // Once server is stopped, ShouldBePatched should patch if update is available, regardless of uptime.
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 1;
        options.MinimumServerUptimeForUpdateSeconds = 120;
        var service = (ServerStatusService)new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-30)); // Only 30 seconds uptime
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var result = await service.ShouldBePatched();

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ShouldBePatched_UpdateAvailable_ServerNeverStarted_ReturnsTrue()
    {
        // Even if server was never started, ShouldBePatched should return true if update is available.
        // The minimum uptime requirement is enforced in ShouldBeStoppedForUpdate for running servers.
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 1;
        options.MinimumServerUptimeForUpdateSeconds = 60;
        var service = (ServerStatusService)new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.MinValue); // Server never started
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var result = await service.ShouldBePatched();

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task GetStatusAsync_UpdateAvailable_MinimumTimeNotExceeded_ReturnsShouldBePatched()
    {
        // Minimum uptime is enforced in ShouldBeStoppedForUpdate. Once server is stopped with update available,
        // ShouldBePatched returns true regardless of uptime.
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 1;
        options.MinimumServerUptimeForUpdateSeconds = 120;
        options.EnableAutoStart = false;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-30)); // Only 30 seconds uptime
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var status = await service.GetStatusAsync();

        // Should return ShouldBePatched because update is available and server is stopped
        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBePatched));
    }

    [Test]
    public async Task GetStatusAsync_UpdateAvailable_ServerStoppedWithUpdate_ReturnsShouldBePatched()
    {
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 1;
        options.MinimumServerUptimeForUpdateSeconds = 30;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-60)); // 60 seconds uptime
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var status = await service.GetStatusAsync();

        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBePatched));
    }

    [Test]
    public async Task GetStatusAsync_UpdateAvailable_ServerNeverStarted_ReturnsShouldBePatched()
    {
        // Once server is stopped with update available, ShouldBePatched returns true regardless of whether
        // server was never started. Minimum uptime requirement is enforced in ShouldBeStoppedForUpdate.
        var options = TestUtils.CreateOptions();
        options.CheckForUpdates = true;
        options.UpdateCheckIntervalSeconds = 1;
        options.MinimumServerUptimeForUpdateSeconds = 60;
        options.EnableAutoStart = false;
        var service = new ServerStatusService(_log, _minecraftService, options, _updateService);
        
        _minecraftService.IsRunning.Returns(false);
        _minecraftService.ServerStartTime.Returns(DateTime.MinValue); // Server never started
        _minecraftService.CurrentVersion.Returns("1.0.0");
        _updateService.NewVersionIsAvailable(Arg.Any<string>()).Returns(Task.FromResult((true, "Update available", "1.0.1")));

        var status = await service.GetStatusAsync();

        // Should return ShouldBePatched because update is available and server is stopped
        Assert.That(status, Is.EqualTo(MineCraftServerStatus.ShouldBePatched));
    }

    #endregion
}

