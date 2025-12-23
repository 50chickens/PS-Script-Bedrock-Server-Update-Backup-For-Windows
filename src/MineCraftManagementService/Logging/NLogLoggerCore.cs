using NLog;

namespace MineCraftManagementService.Logging;

/// <summary>
/// NLog-based implementation of ILog&lt;T&gt; that directly uses NLog without going through ILogger&lt;T&gt;.
/// This prevents duplicate log entries when both NLog and ILogger are wired together.
/// </summary>
public class NLogLoggerCore<T> : ILog<T>
{
    private readonly Logger _logger;

    public NLogLoggerCore()
    {
        _logger = LogManager.GetLogger(typeof(T).FullName ?? typeof(T).Name);
    }

    public void Debug(string message) => _logger.Debug(message);
    public void Debug(Exception ex, string message) => _logger.Debug(ex, message);
    public void Info(string message) => _logger.Info(message);
    public void Warn(string message) => _logger.Warn(message);
    public void Warn(Exception ex, string message) => _logger.Warn(ex, message);
    public void Error(string message) => _logger.Error(message);
    public void Error(Exception ex, string message) => _logger.Error(ex, message);
    public void Trace(string message) => _logger.Trace(message);

}
