using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class MineCraftApiClientTests
{
    private ILog<MineCraftApiClient> _log = null!;
    private HttpClient _httpClient = null!;
    private MineCraftServerOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<MineCraftApiClient>();
        _httpClient = new HttpClient();
        _options = TestUtils.CreateOptions();
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when HttpClient is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_HttpClient_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MineCraftApiClient(null!, _log, _options));
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when log is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_Log_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MineCraftApiClient(_httpClient, null!, _options));
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when options is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_Options_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MineCraftApiClient(_httpClient, _log, null!));
    }

    // Note: GetLatestVersionAsync makes actual HTTP calls to Microsoft API.
    // Full integration testing would require either mocking the HttpClient responses
    // or using a test server. These tests verify constructor validation only.
    // Integration tests or manual testing should verify API communication.
}
