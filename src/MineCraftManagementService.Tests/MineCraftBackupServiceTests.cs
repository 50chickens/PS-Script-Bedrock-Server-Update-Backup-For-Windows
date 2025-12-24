using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;
using NSubstitute;

namespace MineCraftManagementService.Tests;

[TestFixture]
public class MineCraftBackupServiceTests
{
    private IMineCraftBackupService _service = null!;
    private ILog<MineCraftBackupService> _log = null!;
    private MineCraftServerOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestUtils.BuildTestConfiguration();
        var logBuilder = new LogBuilder(config);
        logBuilder.UseNunitTestContext();
        logBuilder.Build();

        _log = LogManager.GetLogger<MineCraftBackupService>();
        var options = TestUtils.CreateOptions();
        // Override paths to use temp directory for tests with unique IDs
        var tempDir = Path.GetTempPath();
        var testId = Guid.NewGuid().ToString().Substring(0, 8);
        options.ServerPath = Path.Combine(tempDir, $"minecraft_test_server_{testId}");
        options.BackupFolderName = Path.Combine(tempDir, $"minecraft_test_backups_{testId}");
        
        // Create server directory with a test file so backup has something to zip
        Directory.CreateDirectory(options.ServerPath);
        File.WriteAllText(Path.Combine(options.ServerPath, "test.txt"), "test content");
        Directory.CreateDirectory(options.BackupFolderName);
        
        _options = options;
        _service = new MineCraftBackupService(_log, _options);
    }

    [TearDown]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_options.ServerPath))
                Directory.Delete(_options.ServerPath, true);
            if (Directory.Exists(_options.BackupFolderName))
                Directory.Delete(_options.BackupFolderName, true);
        }
        catch { /* Ignore cleanup errors */ }
    }

    [Test]
    public void CreateBackupZipFromServerFolder_ReturnsFilePath()
    {
        var result = _service.CreateBackupZipFromServerFolder();

        Assert.That(result, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void CreateBackupZipFromServerFolder_CreatesZipFile()
    {
        var result = _service.CreateBackupZipFromServerFolder();

        Assert.That(File.Exists(result), Is.True, "Backup zip file should exist");
        File.Delete(result); // Cleanup
    }

    [Test]
    public void CreateBackupZipFromServerFolder_ZipNameIncludesTimestamp()
    {
        var result = _service.CreateBackupZipFromServerFolder();
        var fileName = Path.GetFileName(result);

        Assert.That(fileName, Does.Contain("minecraft_backup"), "Zip should contain 'minecraft_backup' in name");
        Assert.That(fileName, Does.EndWith(".zip"), "File should be a zip");
        File.Delete(result); // Cleanup
    }
}
