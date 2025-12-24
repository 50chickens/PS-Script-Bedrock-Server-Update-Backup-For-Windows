using System.Collections.Specialized;
using Common.Logging;
using Common.Logging.Factory;

namespace MineCraftManagementService.Logging
{
    public class NLogLoggerFactoryAdapter : AbstractCachingLoggerFactoryAdapter
    {
        public NLogLoggerFactoryAdapter(NameValueCollection properties)
        {
        }

        protected override ILog CreateLogger(string name)
        {
            return new NLogLogger(NLog.LogManager.GetLogger(name));
        }
    }
}
