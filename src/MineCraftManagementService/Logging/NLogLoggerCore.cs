using System.Reflection;
using Microsoft.Extensions.Logging;
using NLog;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace MineCraftManagementService.Logging
{

    public class NLogLoggerCore<T> : ILog<T>
    {
        private readonly Logger _logger;
        private readonly ILogger<T>? _msLogger;

        /// <summary>
        /// Constructor for use with dependency injection.
        /// </summary>
        public NLogLoggerCore(ILogger<T> msLogger)
        {
            _logger = NLog.LogManager.GetLogger(typeof(T).FullName ?? typeof(T).Name);
            _msLogger = msLogger;
        }

        /// <summary>
        /// Parameterless constructor for use with static LogManager (before DI).
        /// When used this way, logging level checks are not performed (backward compatibility).
        /// </summary>
        public NLogLoggerCore()
        {
            _logger = NLog.LogManager.GetLogger(typeof(T).FullName ?? typeof(T).Name);
            _msLogger = null;
        }

        public void Debug(string message) 
        {
            if (_msLogger == null || _msLogger.IsEnabled(MsLogLevel.Debug))
                _logger.Debug(message);
        }
        
        public void Info(string message) 
        {
            if (_msLogger == null || _msLogger.IsEnabled(MsLogLevel.Information))
                _logger.Info(message);
        }
        
        public void Warn(string message) 
        {
            if (_msLogger == null || _msLogger.IsEnabled(MsLogLevel.Warning))
                _logger.Warn(message);
        }
        
        public void Error(string message) 
        {
            if (_msLogger == null || _msLogger.IsEnabled(MsLogLevel.Error))
                _logger.Error(message);
        }
        
        public void Error(Exception ex, string message) 
        {
            if (_msLogger == null || _msLogger.IsEnabled(MsLogLevel.Error))
                _logger.Error(ex, message);
        }
        
        public void Trace(string message) 
        {
            if (_msLogger == null || _msLogger.IsEnabled(MsLogLevel.Trace))
                _logger.Trace(message);
        }

        public void Info(object payload)
        {
            var evt = CreateEvent(NLog.LogLevel.Info, payload, null);
            _logger.Log(evt);
        }

        public void Info(string message, object payload)
        {
            var evt = CreateEvent(NLog.LogLevel.Info, payload, message);
            _logger.Log(evt);
        }

        private LogEventInfo CreateEvent(NLog.LogLevel level, object? payload, string? message)
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
