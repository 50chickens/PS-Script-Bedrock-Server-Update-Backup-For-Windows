using NLog;
using NLog.Targets;

namespace MineCraftManagementService.Tests
{
    [Target("NUnitTestContext")]
    public class NUnitTestContextTarget : TargetWithLayout
    {
        protected override void Write(LogEventInfo logEvent)
        {
            var message = this.Layout.Render(logEvent);
            TestContext.Progress.WriteLine(message);
        }
    }
}
