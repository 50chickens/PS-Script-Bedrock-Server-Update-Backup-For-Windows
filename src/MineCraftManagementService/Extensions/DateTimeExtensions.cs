namespace MineCraftManagementService.Extensions;

public static class DateTimeExtensions
{
    //checks to see if the server has been running more than the specified auto-shutdown time
    public static bool AutoShutdownTimeExceeded(this DateTime serverStartTime, int autoShutdownAfterSeconds, out int secondsRemaining)
    {
        secondsRemaining = 0;
        if (autoShutdownAfterSeconds <= 0)
            return false;

        var runDuration = DateTime.Now - serverStartTime;
        secondsRemaining = autoShutdownAfterSeconds - (int)runDuration.TotalSeconds;
        return runDuration.TotalSeconds >= autoShutdownAfterSeconds;
    }
}

