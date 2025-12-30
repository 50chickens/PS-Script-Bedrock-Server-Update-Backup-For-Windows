namespace MineCraftManagementService.Interfaces;

public interface IMineCraftSchedulerService
{
    DateTime GetUpdateCheckTime();
    void SetUpdateCheckTime(DateTime time);
    DateTime GetAutoShutdownTime();
    void SetAutoShutdownTime(DateTime time);
    DateTime GetServiceStartedAt();
    void SetServiceStartedAt(DateTime time);
    bool IsUpdateCheckDue();
    bool IsAutoShutdownDue();
    DateTime GetCurrentTime();
    bool IsAutoShutdownTimeSet();
    bool IsUpdateCheckTimeSet();
}
