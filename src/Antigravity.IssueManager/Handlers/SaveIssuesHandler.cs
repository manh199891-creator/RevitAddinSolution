using System;
using System.Collections.Generic;
using Antigravity.IssueManager.Models;
using Antigravity.IssueManager.Services;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Antigravity.IssueManager.Handlers
{
    public class SaveIssuesHandler : IExternalEventHandler
    {
        public List<IssueModel> Issues { get; set; } = new List<IssueModel>();
        public Action<Exception> OnError { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument?.Document;
                if (doc == null) return;

                using (Transaction tx = new Transaction(doc, "Save Antigravity Issues"))
                {
                    tx.Start();
                    IssueStorageService.SaveIssuesToDocument(doc, Issues);
                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        public string GetName()
        {
            return "Save Antigravity Issues";
        }
    }
}
