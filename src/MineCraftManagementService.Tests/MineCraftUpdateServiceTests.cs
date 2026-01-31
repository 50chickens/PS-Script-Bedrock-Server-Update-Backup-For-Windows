using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class MineCraftUpdateServiceTests
{
    private ILog<MineCraftUpdateService> _log = null!;
    private IMineCraftServerService _minecraftService = null!;
    private IMineCraftVersionService _versionService = null!;
    private MineCraftServerOptions _options = null!;
    private MineCraftUpdateService _service = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<MineCraftUpdateService>();
        _minecraftService = Substitute.For<IMineCraftServerService>();
        _versionService = Substitute.For<IMineCraftVersionService>();
        _options = TestUtils.CreateOptions();
        _options.MinimumServerUptimeForUpdateSeconds = 0; // Allow immediate update checks by default

        _service = new MineCraftUpdateService(_log, _minecraftService, _versionService, _options);
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when log is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_Log_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => 
            new MineCraftUpdateService(null!, _minecraftService, _versionService, _options));
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when minecraft service is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_MinecraftService_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => 
            new MineCraftUpdateService(_log, null!, _versionService, _options));
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when version service is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_VersionService_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => 
            new MineCraftUpdateService(_log, _minecraftService, null!, _options));
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when options is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_Options_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => 
            new MineCraftUpdateService(_log, _minecraftService, _versionService, null!));
    }

    /// <summary>
    /// Test: NewVersionIsAvailable returns false when versions match.
    /// Intent: Verify up-to-date detection logic.
    /// Importance: Core feature - prevents unnecessary updates.
    /// </summary>
    [Test]
    public async Task Test_That_NewVersionIsAvailable_Returns_False_When_Versions_Match()
    {
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        var serverDownload = new MineCraftServerDownload { Version = "1.0.0", Url = "http://example.com" };
        _versionService.GetLatestVersionAsync(Arg.Any<CancellationToken>()).Returns(serverDownload);

        var result = await _service.NewVersionIsAvailable("1.0.0");

        Assert.That(result.Item1, Is.False);
        Assert.That(result.Item2, Does.Contain("up to date"));
    }

    /// <summary>
    /// Test: NewVersionIsAvailable returns true when update available.
    /// Intent: Verify update detection logic.
    /// Importance: Core feature - enables automatic updates.
    /// </summary>
    [Test]
    public async Task Test_That_NewVersionIsAvailable_Returns_True_When_Update_Available()
    {
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        var serverDownload = new MineCraftServerDownload { Version = "1.0.1", Url = "http://example.com" };
        _versionService.GetLatestVersionAsync(Arg.Any<CancellationToken>()).Returns(serverDownload);

        var result = await _service.NewVersionIsAvailable("1.0.0");

        Assert.That(result.Item1, Is.True);
        Assert.That(result.Item2, Does.Contain("Update available"));
        Assert.That(result.Item3, Is.EqualTo("1.0.1"));
    }

    /// <summary>
    /// Test: NewVersionIsAvailable returns false when version service returns null.
    /// Intent: Verify error handling for API failures.
    /// Importance: Robustness - handles external service failures gracefully.
    /// </summary>
    [Test]
    public async Task Test_That_NewVersionIsAvailable_Returns_False_When_VersionService_Returns_Null()
    {
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        _versionService.GetLatestVersionAsync(Arg.Any<CancellationToken>()).Returns((MineCraftServerDownload?)null);

        var result = await _service.NewVersionIsAvailable("1.0.0");

        Assert.That(result.Item1, Is.False);
        Assert.That(result.Item2, Does.Contain("Failed to determine latest"));
    }

    /// <summary>
    /// Test: NewVersionIsAvailable skips check when server uptime insufficient.
    /// Intent: Verify minimum uptime requirement logic.
    /// Importance: Stability - prevents update checks on recently started servers.
    /// </summary>
    [Test]
    public async Task Test_That_NewVersionIsAvailable_Skips_When_Server_Uptime_Insufficient()
    {
        _options.MinimumServerUptimeForUpdateSeconds = 300; // 5 minutes
        _service = new MineCraftUpdateService(_log, _minecraftService, _versionService, _options);
        
        _minecraftService.ServerStartTime.Returns(DateTime.Now.AddSeconds(-60)); // Only 1 minute uptime

        var result = await _service.NewVersionIsAvailable("1.0.0");

        Assert.That(result.Item1, Is.False);
        Assert.That(result.Item2, Does.Contain("Update check skipped"));
        await _versionService.DidNotReceive().GetLatestVersionAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Test: NewVersionIsAvailable handles exceptions gracefully.
    /// Intent: Verify exception handling.
    /// Importance: Robustness - prevents crashes from unexpected errors.
    /// </summary>
    [Test]
    public async Task Test_That_NewVersionIsAvailable_Handles_Exceptions()
    {
        _minecraftService.ServerStartTime.Returns(DateTime.Now);
        _versionService.GetLatestVersionAsync(Arg.Any<CancellationToken>())
            .Returns<MineCraftServerDownload>(x => throw new Exception("Test exception"));

        var result = await _service.NewVersionIsAvailable("1.0.0");

        Assert.That(result.Item1, Is.False);
        Assert.That(result.Item2, Does.Contain("Exception"));
    }
}
