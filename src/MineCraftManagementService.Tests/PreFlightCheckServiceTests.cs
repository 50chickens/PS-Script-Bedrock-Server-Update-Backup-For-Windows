using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using NSubstitute;

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

    [Test]
    public async Task CheckAndCleanupAsync_CompletesSuccessfully()
    {
        Assert.DoesNotThrowAsync(async () => await _service.CheckAndCleanupAsync());
    }


}
