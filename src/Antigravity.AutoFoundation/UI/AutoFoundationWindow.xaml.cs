using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Antigravity.AutoFoundation.Services;

namespace Antigravity.AutoFoundation.UI
{
    public partial class AutoFoundationWindow : Window
    {
        private UIApplication _uiapp;

        public AutoFoundationWindow(AutoFoundationViewModel viewModel, UIApplication uiapp)
        {
            InitializeComponent();
            this.DataContext = viewModel;
            _uiapp = uiapp;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnSetOrigin_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                XYZ revitPt = _uiapp.ActiveUIDocument.Selection.PickPoint(ObjectSnapTypes.None, "Select origin point in Revit");
                CadInteropService cadService = new CadInteropService();
                if (cadService.Connect())
                {
                    cadService.SetOriginFromRevitPoint(revitPt);
                    TaskDialog.Show("Set Coordinate Origin", "Coordinate origin successfully mapped!");
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
            this.ShowDialog();
        }

        private void DrawFoundation_Click(object sender, RoutedEventArgs e)
        {
            var _viewModel = this.DataContext as AutoFoundationViewModel;
            if (_viewModel == null) return;

            var uidoc = _uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            if (_viewModel.SelectedFamily == null)
            {
                TaskDialog.Show("Auto Foundation", "Vui lòng load Family Móng (Structural Foundation) trước khi chạy lệnh.");
                return;
            }
            if (_viewModel.SelectedLevel == null) return;

            this.Hide();

            try
            {
                var reference = uidoc.Selection.PickObject(ObjectType.Element, "Select CAD Import/Link");
                var importInstance = doc.GetElement(reference) as ImportInstance;
                
                if (importInstance == null)
                {
                    TaskDialog.Show("Antigravity - Error", "Selected element is not a CAD ImportInstance.");
                    this.ShowDialog();
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
                        this.ShowDialog();
                        return;
                    }

                    var foundationType = _viewModel.SelectedFamily;
                    if (!foundationType.IsActive)
                        foundationType.Activate();

                    double offset = 0;
                    double.TryParse(_viewModel.Offset, out offset);

                    var adapter = new RevitFoundationPlacementAdapter(doc, foundationType, levels, _viewModel.ParamL, _viewModel.ParamW, offset);
                    var orchestrator = new FoundationPlacementOrchestrator(adapter, new CadParserService());
                    
                    string layer = string.IsNullOrEmpty(_viewModel.CadLayer) ? "S-FND" : _viewModel.CadLayer;
                    successCount = orchestrator.ExtractAndPlaceFoundations(importInstance, layer);

                    if (successCount == 0)
                    {
                        tx.RollBack();
                        TaskDialog.Show("Antigravity - Warning", $"No valid foundations could be placed from the CAD layer '{layer}'.");
                        this.ShowDialog();
                        return;
                    }

                    if (tx.Commit() != TransactionStatus.Committed)
                    {
                        TaskDialog.Show("Antigravity - Error", "Transaction failed to commit.");
                        this.ShowDialog();
                        return;
                    }
                }

                TaskDialog.Show("Antigravity - Auto Foundation", $"Successfully placed {successCount} foundations from CAD.");
                this.Close();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                this.ShowDialog();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                this.ShowDialog();
            }
        }
    }
}
