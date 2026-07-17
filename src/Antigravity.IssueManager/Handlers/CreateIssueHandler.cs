using System;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.IssueManager.Models;
using Antigravity.IssueManager.Services;
using Antigravity.IssueManager.UI;

namespace Antigravity.IssueManager.Handlers
{
    public class CreateIssueHandler : IExternalEventHandler
    {
        public bool IsCaptureOnly { get; set; }
        
        public string Title { get; set; }
        public string Level { get; set; }
        public string AssignedTo { get; set; }
        public string Description { get; set; }
        public bool IncludeSectionBoxClipPlanes { get; set; }
        public bool UseSharedCoordinates { get; set; }
        public string Image2DPath { get; set; }
        public string Image3DPath { get; set; }

        public Action<IssueModel> OnIssueCreated { get; set; }
        public Action<string, string> OnCaptured { get; set; }
        public Action<Exception> OnError { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                UIDocument uiDoc = app.ActiveUIDocument;
                if (uiDoc == null) throw new InvalidOperationException("No active Revit document.");
                Document doc = uiDoc.Document;
                if (doc.ActiveView.ViewType != ViewType.ThreeD || !(doc.ActiveView is View3D view3d))
                {
                    throw new InvalidOperationException("Create/Capture Issue only supports 3D views.");
                }

                if (IsCaptureOnly)
                {
                    string snapshotPath = RevitIssueCreator.ExportCurrentViewSnapshot(doc);
                    string levelName = view3d.GenLevel != null ? view3d.GenLevel.Name : "";
                    OnCaptured?.Invoke(snapshotPath, levelName);
                }
                else
                {
                    IssueModel newIssue = RevitIssueCreator.CreateIssueFromCurrentView(
                        app,
                        Title,
                        Description,
                        IncludeSectionBoxClipPlanes,
                        UseSharedCoordinates,
                        Image2DPath,
                        Level,
                        AssignedTo);

                    if (!string.IsNullOrEmpty(Image3DPath) && System.IO.File.Exists(Image3DPath))
                    {
                        if (Image3DPath != newIssue.Viewpoint.SnapshotFilePath)
                        {
                            try { System.IO.File.Delete(newIssue.Viewpoint.SnapshotFilePath); } catch {}
                        }
                        newIssue.Viewpoint.SnapshotFilePath = Image3DPath;
                    }
                    OnIssueCreated?.Invoke(newIssue);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        public string GetName()
        {
            return "Create/Capture Issue Handler";
        }
    }
}
