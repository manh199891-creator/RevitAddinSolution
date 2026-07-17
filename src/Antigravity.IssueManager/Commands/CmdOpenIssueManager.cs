using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.IssueManager.UI;
using Antigravity.IssueManager.Handlers;
using System.Diagnostics;

namespace Antigravity.IssueManager.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdOpenIssueManager : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            // Register External Events
            CreateIssueHandler createIssueHandler = new CreateIssueHandler();
            ExternalEvent createIssueEvent = ExternalEvent.Create(createIssueHandler);

            ShowIssueInModelHandler showIssueHandler = new ShowIssueInModelHandler();
            ExternalEvent showIssueEvent = ExternalEvent.Create(showIssueHandler);

            IsolateClashElementsHandler isolateClashHandler = new IsolateClashElementsHandler();
            ExternalEvent isolateClashEvent = ExternalEvent.Create(isolateClashHandler);

            ModelClashPointHandler modelClashHandler = new ModelClashPointHandler();
            ExternalEvent modelClashEvent = ExternalEvent.Create(modelClashHandler);

            VerifyClashHandler verifyClashHandler = new VerifyClashHandler();
            ExternalEvent verifyClashEvent = ExternalEvent.Create(verifyClashHandler);

            SaveIssuesHandler saveIssuesHandler = new SaveIssuesHandler();
            ExternalEvent saveIssuesEvent = ExternalEvent.Create(saveIssuesHandler);

            // Pass handlers to the window
            IssueManagerWindow window = new IssueManagerWindow(uiApp, 
                createIssueHandler, createIssueEvent, 
                showIssueHandler, showIssueEvent,
                isolateClashHandler, isolateClashEvent,
                modelClashHandler, modelClashEvent,
                verifyClashHandler, verifyClashEvent,
                saveIssuesHandler, saveIssuesEvent);
            
            // Show as Modeless
            window.Show();

            return Result.Succeeded;
        }
    }
}
