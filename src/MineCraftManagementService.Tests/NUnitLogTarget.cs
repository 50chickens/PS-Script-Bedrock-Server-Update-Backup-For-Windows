using NLog.Targets;

namespace MineCraftManagementService.Tests
{
    public class NUnitLogTarget : TargetWithLayout
    {
        public NUnitLogTarget()
        {
            Layout = "${longdate} | ${logger:shortname=true} - ${message} ${exception:format=ToString}";
            Name = "Nunit";
        }
        protected override void Write(NLog.LogEventInfo logEvent)
        {
            // render the layout and write to NUnit's progress output so test runners capture the output
            var text = this.Layout.Render(logEvent);
            TestContext.Progress.WriteLine(text);
        }
    }
}
