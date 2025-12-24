using Common.Logging;
using Common.Logging.Factory;

namespace MineCraftManagementService.Logging
{
    public class NLogLogger : AbstractLogger
    {
        private readonly NLog.Logger _logger;
        public NLogLogger(NLog.Logger logger)
        {
            _logger = logger;
        }

        public override bool IsTraceEnabled => true;
        public override bool IsDebugEnabled => true;

        public override bool IsInfoEnabled => true;

        public override bool IsWarnEnabled => true;

        public override bool IsErrorEnabled => true;

        public override bool IsFatalEnabled => true;

        protected override void WriteInternal(LogLevel level, object message, Exception exception)
        {
            var logEventInfo = new NLog.LogEventInfo
            {
                Level = level.ToNlogLogLevel(),
                Message = message?.ToString(),
                Exception = exception
            };
            _logger.Log(logEventInfo);
        }
    }
}
