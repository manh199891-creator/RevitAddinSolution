using System;
using Autodesk.Revit.UI;
using Antigravity.AutoDimWalls.Models;
using Antigravity.AutoDimWalls.Services;

namespace Antigravity.AutoDimWalls.Services
{
    public enum AutoDimAction
    {
        Create,
        Delete
    }

    public sealed class AutoDimEventHandler : IExternalEventHandler
    {
        public AutoDimAction Action { get; set; }
        public AutoDimOptions Options { get; set; }
        public Action<AutoDimResult> OnCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document == null)
                    return;

                var engine = new AutoDimEngine(uiDoc);
                AutoDimResult result;

                if (Action == AutoDimAction.Create)
                {
                    result = engine.CreateDimensions(Options);
                }
                else
                {
                    result = engine.DeleteCreatedDimensions();
                }

                OnCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                var result = new AutoDimResult
                {
                    Message = "Error: " + ex.Message
                };
                OnCompleted?.Invoke(result);
            }
        }

        public string GetName()
        {
            return "BimTools AutoDim Walls Event";
        }
    }
}
