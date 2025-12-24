using Microsoft.Extensions.Logging;

namespace MineCraftManagementService.Logging
{
    /// <summary>
    /// Adapter that implements the project's ILog<T> by delegating to Microsoft.Extensions.Logging.ILogger<T>.
    /// This allows components to migrate to ILogger<T> while preserving existing ILog<T> usages.
    /// </summary>
    public class LoggerAdapter<T> : ILog<T>
    {
        private readonly ILogger<T> _logger;

        public LoggerAdapter(ILogger<T> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Debug(string message) => _logger.LogDebug(message);
        public void Info(string message) => _logger.LogInformation(message);
        public void Warn(string message) => _logger.LogWarning(message);
        public void Error(string message) => _logger.LogError(message);
        public void Error(Exception ex, string message) => _logger.LogError(ex, message);
        public void Trace(string message) => _logger.LogTrace(message);

    }
}
