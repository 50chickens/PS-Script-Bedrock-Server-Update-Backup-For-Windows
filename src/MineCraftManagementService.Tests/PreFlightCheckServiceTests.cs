using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Services;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class PreFlightCheckServiceTests
{
    private IPreFlightCheckService _service = null!;
    private ILog<PreFlightCheckService> _log = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<PreFlightCheckService>();
        var options = TestUtils.CreateOptions();
        _service = new PreFlightCheckService(_log, options);
    }

    /// <summary>
    /// Test: CheckAndCleanupAsync completes without throwing exceptions.
    /// Intent: Verify that preflight checks can be executed without errors.
    /// Importance: Ensures startup validation runs successfully - prevents initialization failures from breaking the service.
    /// </summary>
    [Test]
    public async Task Test_That_PreflightCheck_Completes_Successfully()
    {
        Assert.DoesNotThrowAsync(async () => await _service.CheckAndCleanupAsync());
    }


}
