using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Antigravity.AutoFoundation.UI;

namespace Antigravity.AutoFoundation.Services
{
    public class AutoFoundationRevitEventHandler : IExternalEventHandler
    {
        private readonly Document _document;
        private readonly AutoFoundationViewModel _viewModel;

        public AutoFoundationRevitEventHandler(Document document, AutoFoundationViewModel viewModel)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            if (_viewModel.SelectedFamily == null)
            {
                TaskDialog.Show("Auto Foundation", "Vui lòng load Family Móng (Structural Foundation) trước khi chạy lệnh.");
                return;
            }
            if (_viewModel.SelectedLevel == null) return;
            if (uidoc == null || !ReferenceEquals(uidoc.Document, _document))
            {
                TaskDialog.Show("Auto Foundation", "The active document changed. Close this window and start Auto Foundation again in the intended document.");
                return;
            }
            var doc = _document;

            try
            {
                var reference = uidoc.Selection.PickObject(ObjectType.Element, "Select CAD Import/Link");
                var importInstance = doc.GetElement(reference) as ImportInstance;
                
                if (importInstance == null)
                {
                    TaskDialog.Show("Antigravity - Error", "Selected element is not a CAD ImportInstance.");
                    return;
                }

                int successCount = 0;

                using (var tx = new Transaction(doc, "Auto Place Foundations"))
                {
                    tx.Start();

                    var levels = new List<Level>();
                    if (_viewModel.SelectedLevel != null) 
                    {
                        levels.Add(_viewModel.SelectedLevel);
                    }
                    else 
                    {
                        levels = new FilteredElementCollector(doc)
                            .OfClass(typeof(Level))
                            .Cast<Level>()
                            .OrderBy(l => l.Elevation)
                            .ToList();
                    }

                    if (!levels.Any())
                    {
                        TaskDialog.Show("Antigravity - Error", "Project contains no levels.");
                        tx.RollBack();
                        return;
                    }

                    var foundationType = _viewModel.SelectedFamily;
                    if (!foundationType.IsActive)
                        foundationType.Activate();

                    var adapter = new RevitFoundationPlacementAdapter(doc, foundationType, levels);
                    var orchestrator = new FoundationPlacementOrchestrator(adapter, new CadParserService());
                    
                    string layer = string.IsNullOrEmpty(_viewModel.CadLayer) ? "S-FND" : _viewModel.CadLayer;
                    successCount = orchestrator.ExtractAndPlaceFoundations(importInstance, layer);

                    if (successCount == 0)
                    {
                        tx.RollBack();
                        TaskDialog.Show("Antigravity - Warning", $"No valid foundations could be placed from the CAD layer '{layer}'.");
                        return;
                    }

                    if (tx.Commit() != TransactionStatus.Committed)
                    {
                        TaskDialog.Show("Antigravity - Error", "Transaction failed to commit.");
                        return;
                    }
                }

                TaskDialog.Show("Antigravity - Auto Foundation", $"Successfully placed {successCount} foundations from CAD.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled pick object
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName()
        {
            return "Auto Foundation Event Handler";
        }
    }
}
