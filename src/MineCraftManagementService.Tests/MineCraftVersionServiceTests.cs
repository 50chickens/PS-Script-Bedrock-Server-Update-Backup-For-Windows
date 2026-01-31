using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class MineCraftVersionServiceTests
{
    private ILog<MineCraftVersionService> _log = null!;
    private MineCraftServerOptions _options = null!;
    private MineCraftVersionService _service = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<MineCraftVersionService>();
        _options = TestUtils.CreateOptions();
        _service = new MineCraftVersionService(_log, _options);
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when log is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_Log_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MineCraftVersionService(null!, _options));
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when options is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_Options_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MineCraftVersionService(_log, null!));
    }

    // Note: GetLatestVersionAsync makes actual HTTP calls and cannot be easily tested
    // without mocking HttpClient. This will be addressed in Part 3 when we refactor
    // to use IHttpClientFactory and create IMineCraftApiClient interface.
    // Integration tests or manual testing should verify API communication.
}
