using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.Core.Services;

namespace Antigravity.CheckFloorElevation.Services
{
    public class HostFloorViewService
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly FloorCollectorService _collector;

        public HostFloorViewService(UIDocument uiDoc, FloorCollectorService collector)
        {
            _uiDoc = uiDoc;
            _doc = uiDoc.Document;
            _collector = collector;
        }

        public void ShowHostFloorIn3D(int hostFloorId)
        {
            ElementId floorId = new ElementId((long)hostFloorId);
            Element floor = _doc.GetElement(floorId);
            if (floor == null)
                throw new InvalidOperationException("Host floor was not found in the active document.");

            View3D view3D = _collector.FindSuitable3DView(_uiDoc.ActiveView);
            if (view3D == null)
                throw new InvalidOperationException("No suitable 3D view was found. Create a printable 3D view and try again.");

            BoundingBoxXYZ elementBox = floor.get_BoundingBox(null);
            if (elementBox == null)
                throw new InvalidOperationException("Host floor has no bounding box.");

            _uiDoc.ActiveView = view3D;

            using (var transaction = new Transaction(_doc, "Show host floor in 3D"))
            {
                transaction.Start();

                try
                {
                    if (view3D.IsTemporaryHideIsolateActive())
                        view3D.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                }
                catch (Exception ex)
                {
                    AppLogger.Warning("[CheckFloorElevation] Could not clear previous temporary isolate: " + ex.Message);
                }

                view3D.IsSectionBoxActive = true;
                view3D.SetSectionBox(CreateExpandedSectionBox(elementBox, 3.0));
                view3D.IsolateElementsTemporary(new List<ElementId> { floorId });

                transaction.Commit();
            }

            _uiDoc.Selection.SetElementIds(new List<ElementId> { floorId });
            _uiDoc.ShowElements(floorId);
        }

        private static BoundingBoxXYZ CreateExpandedSectionBox(BoundingBoxXYZ sourceBox, double marginFeet)
        {
            return new BoundingBoxXYZ
            {
                Min = new XYZ(
                    sourceBox.Min.X - marginFeet,
                    sourceBox.Min.Y - marginFeet,
                    sourceBox.Min.Z - marginFeet),
                Max = new XYZ(
                    sourceBox.Max.X + marginFeet,
                    sourceBox.Max.Y + marginFeet,
                    sourceBox.Max.Z + marginFeet)
            };
        }
    }
}
