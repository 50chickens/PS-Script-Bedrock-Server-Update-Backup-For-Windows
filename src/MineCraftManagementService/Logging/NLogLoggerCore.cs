using System.Reflection;
using NLog;

namespace MineCraftManagementService.Logging
{

    public class NLogLoggerCore<T> : ILog<T>
    {
        private readonly Logger _logger;

        public NLogLoggerCore()
        {
            _logger = NLog.LogManager.GetLogger(typeof(T).FullName ?? typeof(T).Name);
        }

        public void Debug(string message) => _logger.Debug(message);
        public void Info(string message) => _logger.Info(message);
        public void Warn(string message) => _logger.Warn(message);
        public void Error(string message) => _logger.Error(message);
        public void Error(Exception ex, string message) => _logger.Error(ex, message);
        public void Trace(string message) => _logger.Trace(message);

        public void Info(object payload)
        {
            var evt = CreateEvent(LogLevel.Info, payload, null);
            _logger.Log(evt);
        }

        public void Info(string message, object payload)
        {
            var evt = CreateEvent(LogLevel.Info, payload, message);
            _logger.Log(evt);
        }

        private LogEventInfo CreateEvent(LogLevel level, object? payload, string? message)
        {
            var messageStr = message;
            if (messageStr == null)
                messageStr = string.Empty;
            var evt = new LogEventInfo(level, _logger.Name, messageStr);
            if (payload != null)
            {
                foreach (var kv in ToDictionary(payload))
                {
                    var v = kv.Value;
                    if (v == null)
                        v = string.Empty;
                    evt.Properties[kv.Key] = v;
                }
            }
            return evt;
        }

        private IDictionary<string, object?> ToDictionary(object payload)
        {
            var dict = new Dictionary<string, object?>();
            var t = payload.GetType();
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                dict[p.Name] = p.GetValue(payload);
            }
            return dict;
        }
    }
}
