using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class MinecraftServerPatchServiceTests
{
    private IMinecraftServerPatchService _service = null!;
    private ILog<MinecraftServerPatchService> _log = null!;
    private IMineCraftUpdateDownloaderService _downloader = null!;
    private MineCraftServerOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<MinecraftServerPatchService>();
        _downloader = Substitute.For<IMineCraftUpdateDownloaderService>();
        _options = TestUtils.CreateOptions();

        _service = new MinecraftServerPatchService(_log, _downloader, _options);
    }

}


