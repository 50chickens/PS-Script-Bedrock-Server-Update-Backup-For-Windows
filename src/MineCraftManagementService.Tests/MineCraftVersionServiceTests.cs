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
    private IMineCraftApiClient _apiClient = null!;
    private MineCraftVersionService _service = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<MineCraftVersionService>();
        _apiClient = Substitute.For<IMineCraftApiClient>();
        _service = new MineCraftVersionService(_log, _apiClient);
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when log is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_Log_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MineCraftVersionService(null!, _apiClient));
    }

    /// <summary>
    /// Test: Constructor throws ArgumentNullException when API client is null.
    /// Intent: Verify dependency validation.
    /// Importance: Error handling - prevents null reference issues.
    /// </summary>
    [Test]
    public void Test_That_Constructor_Throws_When_ApiClient_IsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MineCraftVersionService(_log, null!));
    }

    /// <summary>
    /// Test: GetLatestVersionAsync delegates to API client.
    /// Intent: Verify service delegates to API client correctly.
    /// Importance: Integration - ensures proper use of abstraction.
    /// </summary>
    [Test]
    public async Task Test_That_GetLatestVersionAsync_Delegates_To_ApiClient()
    {
        var expectedResult = new MineCraftServerDownload { Version = "1.0.0", Url = "http://example.com" };
        _apiClient.GetLatestVersionAsync(Arg.Any<CancellationToken>()).Returns(expectedResult);

        var result = await _service.GetLatestVersionAsync(CancellationToken.None);

        Assert.That(result, Is.EqualTo(expectedResult));
        await _apiClient.Received(1).GetLatestVersionAsync(Arg.Any<CancellationToken>());
    }
}
