using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class MineCraftSchedulerServiceTests
{
    private ILog<MineCraftSchedulerService> _log = null!;
    private MineCraftServerOptions _options = null!;
    private MineCraftSchedulerService _service = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<MineCraftSchedulerService>();
        _options = TestUtils.CreateOptions();
        _service = new MineCraftSchedulerService(_log, _options);
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when log is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_Log_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MineCraftSchedulerService(null!, _options));
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when options is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_Options_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MineCraftSchedulerService(_log, null!));
    }

    /// <summary>
    /// Test: GetUpdateCheckTime returns DateTime.MinValue initially.
    /// Intent: Verify initial state of update check scheduling.
    /// Importance: Initialization - ensures safe default state.
    /// </summary>
    [Test]
    public void Test_That_GetUpdateCheckTime_Returns_MinValue_Initially()
    {
        Assert.That(_service.GetUpdateCheckTime(), Is.EqualTo(DateTime.MinValue));
    }

    /// <summary>
    /// Test: GetAutoShutdownTime returns DateTime.MinValue initially.
    /// Intent: Verify initial state of auto-shutdown scheduling.
    /// Importance: Initialization - ensures safe default state.
    /// </summary>
    [Test]
    public void Test_That_GetAutoShutdownTime_Returns_MinValue_Initially()
    {
        Assert.That(_service.GetAutoShutdownTime(), Is.EqualTo(DateTime.MinValue));
    }

    /// <summary>
    /// Test: GetServiceStartedAt returns DateTime.MinValue initially.
    /// Intent: Verify initial state of service start tracking.
    /// Importance: Initialization - ensures safe default state.
    /// </summary>
    [Test]
    public void Test_That_GetServiceStartedAt_Returns_MinValue_Initially()
    {
        Assert.That(_service.GetServiceStartedAt(), Is.EqualTo(DateTime.MinValue));
    }

    /// <summary>
    /// Test: SetUpdateCheckTime stores and retrieves time correctly.
    /// Intent: Verify update check time storage.
    /// Importance: State management - enables scheduled update checks.
    /// </summary>
    [Test]
    public void Test_That_SetUpdateCheckTime_Stores_Time()
    {
        var testTime = DateTime.Now.AddHours(1);
        _service.SetUpdateCheckTime(testTime);
        Assert.That(_service.GetUpdateCheckTime(), Is.EqualTo(testTime));
    }

    /// <summary>
    /// Test: SetAutoShutdownTime stores and retrieves time correctly.
    /// Intent: Verify auto-shutdown time storage.
    /// Importance: State management - enables scheduled shutdowns.
    /// </summary>
    [Test]
    public void Test_That_SetAutoShutdownTime_Stores_Time()
    {
        var testTime = DateTime.Now.AddHours(2);
        _service.SetAutoShutdownTime(testTime);
        Assert.That(_service.GetAutoShutdownTime(), Is.EqualTo(testTime));
    }

    /// <summary>
    /// Test: SetServiceStartedAt stores and retrieves time correctly.
    /// Intent: Verify service start time storage.
    /// Importance: State management - enables uptime tracking.
    /// </summary>
    [Test]
    public void Test_That_SetServiceStartedAt_Stores_Time()
    {
        var testTime = DateTime.Now.AddMinutes(-30);
        _service.SetServiceStartedAt(testTime);
        Assert.That(_service.GetServiceStartedAt(), Is.EqualTo(testTime));
    }

    /// <summary>
    /// Test: IsUpdateCheckTimeSet returns false initially.
    /// Intent: Verify initial state detection.
    /// Importance: Logic control - prevents actions before scheduling.
    /// </summary>
    [Test]
    public void Test_That_IsUpdateCheckTimeSet_Returns_False_Initially()
    {
        Assert.That(_service.IsUpdateCheckTimeSet(), Is.False);
    }

    /// <summary>
    /// Test: IsUpdateCheckTimeSet returns true after setting time.
    /// Intent: Verify scheduled state detection.
    /// Importance: Logic control - enables scheduled actions.
    /// </summary>
    [Test]
    public void Test_That_IsUpdateCheckTimeSet_Returns_True_After_Setting()
    {
        _service.SetUpdateCheckTime(DateTime.Now.AddHours(1));
        Assert.That(_service.IsUpdateCheckTimeSet(), Is.True);
    }

    /// <summary>
    /// Test: IsAutoShutdownTimeSet returns false initially.
    /// Intent: Verify initial state detection.
    /// Importance: Logic control - prevents actions before scheduling.
    /// </summary>
    [Test]
    public void Test_That_IsAutoShutdownTimeSet_Returns_False_Initially()
    {
        Assert.That(_service.IsAutoShutdownTimeSet(), Is.False);
    }

    /// <summary>
    /// Test: IsAutoShutdownTimeSet returns true after setting time.
    /// Intent: Verify scheduled state detection.
    /// Importance: Logic control - enables scheduled actions.
    /// </summary>
    [Test]
    public void Test_That_IsAutoShutdownTimeSet_Returns_True_After_Setting()
    {
        _service.SetAutoShutdownTime(DateTime.Now.AddHours(1));
        Assert.That(_service.IsAutoShutdownTimeSet(), Is.True);
    }

    /// <summary>
    /// Test: GetCurrentTime returns current DateTime.
    /// Intent: Verify time provider functionality.
    /// Importance: Testability - provides mockable time source.
    /// </summary>
    [Test]
    public void Test_That_GetCurrentTime_Returns_Current_DateTime()
    {
        var before = DateTime.Now;
        var current = _service.GetCurrentTime();
        var after = DateTime.Now;

        Assert.That(current, Is.GreaterThanOrEqualTo(before));
        Assert.That(current, Is.LessThanOrEqualTo(after));
    }

    /// <summary>
    /// Test: IsUpdateCheckDue returns false when time not reached.
    /// Intent: Verify scheduling logic for future times.
    /// Importance: Timing control - prevents premature actions.
    /// </summary>
    [Test]
    public void Test_That_IsUpdateCheckDue_Returns_False_When_Time_Not_Reached()
    {
        _service.SetUpdateCheckTime(DateTime.Now.AddHours(1));
        Assert.That(_service.IsUpdateCheckDue(), Is.False);
    }

    /// <summary>
    /// Test: IsUpdateCheckDue returns true when time reached.
    /// Intent: Verify scheduling logic for past times.
    /// Importance: Timing control - triggers actions at correct time.
    /// </summary>
    [Test]
    public void Test_That_IsUpdateCheckDue_Returns_True_When_Time_Reached()
    {
        _service.SetUpdateCheckTime(DateTime.Now.AddSeconds(-1));
        Assert.That(_service.IsUpdateCheckDue(), Is.True);
    }

    /// <summary>
    /// Test: IsAutoShutdownDue returns false when time not reached.
    /// Intent: Verify scheduling logic for future times.
    /// Importance: Timing control - prevents premature shutdowns.
    /// </summary>
    [Test]
    public void Test_That_IsAutoShutdownDue_Returns_False_When_Time_Not_Reached()
    {
        _service.SetAutoShutdownTime(DateTime.Now.AddHours(1));
        Assert.That(_service.IsAutoShutdownDue(), Is.False);
    }

    /// <summary>
    /// Test: IsAutoShutdownDue returns true when time reached.
    /// Intent: Verify scheduling logic for past times.
    /// Importance: Timing control - triggers shutdowns at correct time.
    /// </summary>
    [Test]
    public void Test_That_IsAutoShutdownDue_Returns_True_When_Time_Reached()
    {
        _service.SetAutoShutdownTime(DateTime.Now.AddSeconds(-1));
        Assert.That(_service.IsAutoShutdownDue(), Is.True);
    }
}
