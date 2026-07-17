using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using Antigravity.Autojoin.Models;

namespace Antigravity.Autojoin.Services
{
    public class JoinEventHandler : IExternalEventHandler
    {
        // ── Input (set trước khi Raise) ──
        public JoinAction       Action { get; set; }
        public IList<JoinRule>  Rules  { get; set; }
        public JoinScope        Scope  { get; set; }

        // ── Output (đọc sau khi hoàn thành) ──
        public JoinResult Result { get; private set; }
        public Exception  Error  { get; private set; }

        // ── Callback khi hoàn thành ──
        public event Action<JoinResult, Exception> Completed;

        public void Execute(UIApplication app)
        {
            Result = null;
            Error  = null;

            try
            {
                var uidoc = app.ActiveUIDocument;
                if (uidoc == null) return;

                if (Action == JoinAction.Join)
                    Result = AutoJoinService.ExecuteJoin(uidoc.Document, uidoc, Rules, Scope);
                else
                    Result = AutoJoinService.ExecuteUnjoin(uidoc.Document, uidoc, Rules, Scope);
            }
            catch (Exception ex)
            {
                Error = ex;
            }

            Completed?.Invoke(Result, Error);
        }

        public string GetName() => "AutoJoin.JoinEventHandler";
    }
}
