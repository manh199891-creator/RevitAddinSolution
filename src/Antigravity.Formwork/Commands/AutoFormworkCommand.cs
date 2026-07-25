using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.Formwork.Core.Models;
using Antigravity.Formwork.Core.Solvers;
using Antigravity.Formwork.Services;
using Antigravity.Formwork.UI;

namespace Antigravity.Formwork.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoFormworkCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                var placer = new RevitFormworkPlacer(doc);
                if (!placer.CanPlace)
                {
                    TaskDialog.Show("Auto Formwork", "Cannot find a generic formwork panel family. Please load a family with 'Formwork' or 'Panel' in its name.");
                    return Result.Failed;
                }

                // Show UI
                var systems = new List<FormworkPanelSystem> { FormworkPanelSystem.CreateGenericSystem() };
                var window = new AutoFormworkWindow(systems);
                window.ShowDialog();

                if (!window.IsConfirmed)
                    return Result.Cancelled;

                var selectedSystem = window.SelectedSystem;
                var solver = new PanelizationSolver(selectedSystem);

                // Get selected walls or all walls if none selected
                var selectedIds = uidoc.Selection.GetElementIds();
                IEnumerable<Element> walls;

                if (selectedIds.Count > 0)
                {
                    walls = selectedIds.Select(id => doc.GetElement(id)).Where(e => e is Wall);
                }
                else
                {
                    walls = new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .OfClass(typeof(Wall))
                        .ToElements();
                }

                if (!walls.Any())
                {
                    TaskDialog.Show("Auto Formwork", "No walls found in selection or active view.");
                    return Result.Failed;
                }

                int count = 0;
                foreach (Wall wall in walls)
                {
                    // Basic extraction for straight walls (MVP)
                    var locationCurve = wall.Location as LocationCurve;
                    if (locationCurve == null || !(locationCurve.Curve is Line line)) continue;

                    double height = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 3000 / 304.8;
                    double baseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0;
                    
                    var levelId = wall.LevelId;
                    double baseElevation = 0;
                    if (levelId != ElementId.InvalidElementId)
                    {
                        var level = doc.GetElement(levelId) as Level;
                        if (level != null) baseElevation = level.Elevation;
                    }

                    baseElevation += baseOffset;

                    // Convert to mm for Core
                    var hostDto = new ConcreteHostDto
                    {
                        HostUniqueId = wall.UniqueId,
                        FaceKey = "Centerline", // Simplified for MVP
                        CycleId = "Cycle-1",
                        StartX = line.GetEndPoint(0).X * 304.8,
                        StartY = line.GetEndPoint(0).Y * 304.8,
                        EndX = line.GetEndPoint(1).X * 304.8,
                        EndY = line.GetEndPoint(1).Y * 304.8,
                        BaseElevation = baseElevation * 304.8,
                        Height = height * 304.8
                    };

                    var result = solver.Solve(hostDto);
                    placer.PlacePanels(result);
                    count++;
                }

                TaskDialog.Show("Auto Formwork", $"Successfully generated formwork for {count} walls.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
