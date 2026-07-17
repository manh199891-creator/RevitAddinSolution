using System;
using Autodesk.Revit.UI;
using Antigravity.IssueManager.Models;
using Antigravity.IssueManager.Services;

namespace Antigravity.IssueManager.Handlers
{
    public class ShowIssueInModelHandler : IExternalEventHandler
    {
        public ViewpointModel Viewpoint { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                RevitCameraSync.SyncCamera(app, Viewpoint);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowIssueInModelHandler Error: {ex.Message}");
            }
        }

        public string GetName()
        {
            return "Show Issue In Model Handler";
        }
    }
}
