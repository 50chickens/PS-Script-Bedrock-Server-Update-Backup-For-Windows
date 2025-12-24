using Microsoft.Extensions.Logging;

namespace MineCraftManagementService.Logging;

public class NLogAdapter<T> : ILog<T>
{
    private readonly ILogger<T> _logger;

    public NLogAdapter(ILogger<T> logger)
    {
        _logger = logger;
    }

    public void Info(string message) => _logger.LogInformation(message);
    public void Debug(string message) => _logger.LogDebug(message);
    public void Warn(string message) => _logger.LogWarning(message);
    public void Error(Exception ex, string? message = null) => _logger.LogError(ex, message);
    public void Error(string message) => _logger.LogError(message);
    public void Trace(string message) => _logger.LogTrace(message);
}
