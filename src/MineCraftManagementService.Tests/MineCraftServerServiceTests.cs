using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class MineCraftServerServiceTests
{
    private ILog<MineCraftServerService> _log = null!;
    private MineCraftServerOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<MineCraftServerService>();
        _options = TestUtils.CreateOptions();
    }

    /// <summary>
    /// Test: Version defaults to "unknown" when no version info available.
    /// Intent: Verify CurrentVersion property returns safe default.
    /// Importance: Baseline - ensures version property works without server output.
    /// </summary>
    [Test]
    public void Test_That_CurrentVersion_Defaults_To_Unknown()
    {
        var service = new MineCraftServerService(_log, _options);
        Assert.That(service.CurrentVersion, Is.EqualTo("unknown"));
    }

    /// <summary>
    /// Test: IsRunning returns false when server not started.
    /// Intent: Verify initial state before server start.
    /// Importance: State management - ensures correct initial state.
    /// </summary>
    [Test]
    public void Test_That_IsRunning_Returns_False_Initially()
    {
        var service = new MineCraftServerService(_log, _options);
        Assert.That(service.IsRunning, Is.False);
    }

    /// <summary>
    /// Test: ServerStartTime returns DateTime.MinValue when server not started.
    /// Intent: Verify start time tracking before server launch.
    /// Importance: Initialization - ensures timing properties have safe defaults.
    /// </summary>
    [Test]
    public void Test_That_ServerStartTime_Is_MinValue_Initially()
    {
        var service = new MineCraftServerService(_log, _options);
        Assert.That(service.ServerStartTime, Is.EqualTo(DateTime.MinValue));
    }

    /// <summary>
    /// Test: Service constructor throws ArgumentNullException when log is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_Log_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MineCraftServerService(null!, _options));
    }

    /// <summary>
    /// Test: Service constructor throws ArgumentNullException when options is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_Options_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MineCraftServerService(_log, null!));
    }

    // Note: Additional tests for process lifecycle management (start/stop/graceful shutdown)
    // cannot be fully tested in unit tests without mocking Process APIs, which would require
    // refactoring to use IProcessManager interface. These will be covered in integration tests
    // or after Part 3 refactoring is complete.
}
