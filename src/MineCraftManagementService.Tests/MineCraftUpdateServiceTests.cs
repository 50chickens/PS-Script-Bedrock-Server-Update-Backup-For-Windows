using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class MineCraftUpdateServiceTests
{
    private IMineCraftUpdateService _service = null!;
    private ILog<MineCraftUpdateService> _log = null!;
    private IMineCraftServerService _minecraftService = null!;
    private IMineCraftVersionService _versionService = null!;
    private MineCraftServerOptions _options = null!;

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

        _service = new MineCraftUpdateService(_log, _minecraftService, _versionService, _options);
    }


}
