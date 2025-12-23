namespace MineCraftManagementService.Logging;

/// <summary>
/// Static log manager for retrieving loggers without DI.
/// Provides LogManager.GetLogger&lt;T&gt;() for convenient access.
/// </summary>
public static class LogProvider
{
    public static ILog<T> GetLogger<T>()
    {
        return new NLogLoggerCore<T>();
    }
}
