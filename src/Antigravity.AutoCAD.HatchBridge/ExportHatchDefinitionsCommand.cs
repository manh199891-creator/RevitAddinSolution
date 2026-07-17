using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Antigravity.HatchPatterns.Contracts;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;

namespace Antigravity.AutoCAD.HatchBridge
{
    public class ExportHatchDefinitionsCommand
    {
        [CommandMethod("AGHATCHEXPORT")]
        public void Execute()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                PromptResult pr = ed.GetString("\nEnter Request ID: ");
                if (pr.Status != PromptStatus.OK) return;

                string requestId = pr.StringResult.Trim();
                if (string.IsNullOrEmpty(requestId)) return;

                string requestFolder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Antigravity", "HatchBridge", "requests");
                string requestFile = Path.Combine(requestFolder, $"{requestId}.json");

                if (!File.Exists(requestFile))
                {
                    ed.WriteMessage($"\nRequest file not found: {requestFile}");
                    return;
                }

                string jsonContent = File.ReadAllText(requestFile);
                var request = JsonConvert.DeserializeObject<HatchBridgeRequest>(jsonContent);

                var reader = new ManagedHatchDefinitionReader(doc);
                var response = reader.ReadHatchDefinitions(request);

                var exchange = new BridgeFileExchange();
                exchange.ExportResponse(response);

                ed.WriteMessage("\nHatch export completed successfully.");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nError during hatch export: {ex.Message}");
            }
        }
    }
}
