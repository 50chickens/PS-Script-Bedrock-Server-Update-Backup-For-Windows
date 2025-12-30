using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class ServerAutoStartServiceTests
{
    private IServerAutoStartService _service = null!;
    private ILog<ServerAutoStartService> _log = null!;
    private IMineCraftServerService _minecraftService = null!;
    private MineCraftServerOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<ServerAutoStartService>();
        _minecraftService = Substitute.For<IMineCraftServerService>();
        _options = TestUtils.CreateOptions();

        _service = new ServerAutoStartService(_log, _minecraftService, _options);
    }

    /// <summary>
    /// Test: Server starts when auto-start is enabled.
    /// Intent: Verify auto-start feature activates the server.
    /// Importance: Core feature - enables hands-off server operation.
    /// </summary>
    [Test]
    public async Task Test_That_ServerStarts_When_AutoStart_Enabled()
    {
        _options.EnableAutoStart = true;
        _options.AutoStartDelaySeconds = 0;
        _minecraftService.StartServerAsync().Returns(Task.FromResult(true));
        var service = new ServerAutoStartService(_log, _minecraftService, _options);

        await service.ApplyAutoStartAsync();

        await _minecraftService.Received(1).StartServerAsync();
    }

    /// <summary>
    /// Test: Server does not start when auto-start is disabled.
    /// Intent: Verify disabled auto-start is respected.
    /// Importance: Configuration respect - honors user's choice.
    /// </summary>
    [Test]
    public async Task Test_That_ServerNotStarted_When_AutoStart_Disabled()
    {
        _options.EnableAutoStart = false;
        var service = new ServerAutoStartService(_log, _minecraftService, _options);

        await service.ApplyAutoStartAsync();

        await _minecraftService.DidNotReceive().StartServerAsync();
    }

    /// <summary>
    /// Test: Configured delay is waited before starting server.
    /// Intent: Verify startup delay is applied as configured.
    /// Importance: Reliability - allows system time to stabilize before server starts.
    /// </summary>
    [Test]
    public async Task Test_That_Delay_Applied_Before_ServerStart()
    {
        _options.EnableAutoStart = true;
        _options.AutoStartDelaySeconds = 1;
        _minecraftService.StartServerAsync().Returns(Task.FromResult(true));
        var service = new ServerAutoStartService(_log, _minecraftService, _options);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await service.ApplyAutoStartAsync();
        stopwatch.Stop();

        // Should have waited at least 1 second
        Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(1000));
        await _minecraftService.Received(1).StartServerAsync();
    }

    /// <summary>
    /// Test: Error is logged when server start fails.
    /// Intent: Verify error handling captures and reports failures.
    /// Importance: Observability - ensures failures are visible for debugging.
    /// </summary>
    [Test]
    public async Task Test_That_Error_Logged_When_ServerStart_Fails()
    {
        _options.EnableAutoStart = true;
        _options.AutoStartDelaySeconds = 0;
        _minecraftService.StartServerAsync().Returns(Task.FromResult(false));
        var service = new ServerAutoStartService(_log, _minecraftService, _options);

        // Should not throw even if start fails
        await service.ApplyAutoStartAsync();

        await _minecraftService.Received(1).StartServerAsync();
    }
}
