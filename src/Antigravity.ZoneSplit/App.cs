using System.Diagnostics;
using Autodesk.Revit.UI;

namespace Antigravity.ZoneSplit
{
    /// <summary>
    /// Entry point for ZoneSplit BIM addin.
    /// skipped ribbon tab registration (managed by Antigravity.Main).
    /// </summary>
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            Trace.WriteLine("[ZoneSplit] OnStartup skipped ribbon registration (managed by Antigravity.Main).");
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            Trace.WriteLine("[ZoneSplit] OnShutdown — cleanup complete.");
            return Result.Succeeded;
        }
    }
}
