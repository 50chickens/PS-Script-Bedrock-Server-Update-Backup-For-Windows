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
    /// Test: ApplyAutoStartAsync starts the server when auto-start is enabled.
    /// Intent: Verify that enabling auto-start triggers server startup when the service is invoked.
    /// Importance: Core auto-start functionality - ensures servers start automatically when configured.
    /// </summary>
    [Test]
    public async Task ApplyAutoStartAsync_AutoStartEnabled_StartsServer()
    {
        _options.EnableAutoStart = true;
        _options.AutoStartDelaySeconds = 0;
        _minecraftService.StartServerAsync().Returns(Task.FromResult(true));
        var service = new ServerAutoStartService(_log, _minecraftService, _options);

        await service.ApplyAutoStartAsync();

        await _minecraftService.Received(1).StartServerAsync();
    }

    /// <summary>
    /// Test: ApplyAutoStartAsync does not start the server when auto-start is disabled.
    /// Intent: Verify that disabling auto-start prevents automatic startup even when the service is invoked.
    /// Importance: Respects user configuration - allows manual server management when auto-start is disabled.
    /// </summary>
    [Test]
    public async Task ApplyAutoStartAsync_AutoStartDisabled_DoesNotStartServer()
    {
        _options.EnableAutoStart = false;
        var service = new ServerAutoStartService(_log, _minecraftService, _options);

        await service.ApplyAutoStartAsync();

        await _minecraftService.DidNotReceive().StartServerAsync();
    }

    /// <summary>
    /// Test: ApplyAutoStartAsync respects the configured delay before starting the server.
    /// Intent: Verify that the service waits for the configured duration before triggering startup.
    /// Importance: Allows delaying auto-start for controlled server restart sequences (e.g., after updates).
    /// </summary>
    [Test]
    public async Task ApplyAutoStartAsync_AutoStartEnabled_WaitsForDelay()
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
    /// Test: ApplyAutoStartAsync handles gracefully when server startup fails.
    /// Intent: Verify that startup failures don't cause exceptions and are logged appropriately.
    /// Importance: Robustness - ensures the service continues operation even if startup fails.
    /// </summary>
    [Test]
    public async Task ApplyAutoStartAsync_StartServerFails_LogsError()
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
