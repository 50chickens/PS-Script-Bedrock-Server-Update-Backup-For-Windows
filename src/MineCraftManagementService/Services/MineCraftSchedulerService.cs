using MineCraftManagementService.Logging;
using MineCraftManagementService.Models;

namespace MineCraftManagementService.Interfaces;

public class MineCraftSchedulerService : IMineCraftSchedulerService
{
    private DateTime _updateCheckTime = DateTime.MinValue;
    private DateTime _autoShutdownTime = DateTime.MinValue;
    private DateTime _serviceStartedAt = DateTime.MinValue;
    private ILog<MineCraftSchedulerService>? _log;
    private MineCraftServerOptions _options;
    public MineCraftSchedulerService(
        ILog<MineCraftSchedulerService> log,
        MineCraftServerOptions options)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
    public DateTime GetUpdateCheckTime()
    {
        return _updateCheckTime;
    }
    public void SetUpdateCheckTime(DateTime time)
    {
        _updateCheckTime = time;
    }
    public DateTime GetAutoShutdownTime()
    {
        return _autoShutdownTime;
    }
    public void SetAutoShutdownTime(DateTime time)
    {
        _autoShutdownTime = time;
    }
    public DateTime GetServiceStartedAt()
    {
        return _serviceStartedAt;
    }
    public void SetServiceStartedAt(DateTime time)
    {
        _serviceStartedAt = time;
    }
    public bool IsUpdateCheckDue()
    {
        return DateTime.Now >= _updateCheckTime;
    }
    public bool IsAutoShutdownDue()
    {
        return DateTime.Now >= _autoShutdownTime;
    }
    public DateTime GetCurrentTime()
    {
        return DateTime.Now;
    }
    public bool IsAutoShutdownTimeSet()
    {
        return _autoShutdownTime != DateTime.MinValue;
    }
    public bool IsUpdateCheckTimeSet()
    {
        return _updateCheckTime != DateTime.MinValue;
    }
}