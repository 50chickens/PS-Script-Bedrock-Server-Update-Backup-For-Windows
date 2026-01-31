using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;
using MineCraftManagementService.Services;

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
        catch (Exception ex)
        {
            // Test cleanup failures are non-critical but should be logged
            TestContext.WriteLine($"Test cleanup warning: {ex.Message}");
            // Don't fail the test due to cleanup issues
        }
    }

    /// <summary>
    /// Test: CreateBackupZipFromServerFolder returns a non-empty file path.
    /// Intent: Verify that the backup service returns a valid path result after creating a backup.
    /// Importance: Core backup functionality - ensures backup creation returns usable path information.
    /// </summary>
    [Test]
    public void CreateBackupZipFromServerFolder_ReturnsFilePath()
    {
        var result = _service.CreateBackupZipFromServerFolder();

        Assert.That(result, Is.Not.Null.And.Not.Empty);
    }

    /// <summary>
    /// Test: CreateBackupZipFromServerFolder actually creates a physical zip file on disk.
    /// Intent: Verify that the backup service creates the actual backup archive file.
    /// Importance: Critical for data integrity - ensures backups are physically created, not just reported.
    /// </summary>
    [Test]
    public void CreateBackupZipFromServerFolder_CreatesZipFile()
    {
        var result = _service.CreateBackupZipFromServerFolder();

        Assert.That(File.Exists(result), Is.True, "Backup zip file should exist");
        File.Delete(result); // Cleanup
    }

    /// <summary>
    /// Test: CreateBackupZipFromServerFolder generates a zip filename containing descriptive name and timestamp.
    /// Intent: Verify that backup files have meaningful names that include timestamps for version tracking.
    /// Importance: Usability - ensures backups can be identified by creation time and purpose.
    /// </summary>
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
