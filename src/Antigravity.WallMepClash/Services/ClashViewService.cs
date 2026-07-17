using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.Core.Services;

namespace Antigravity.WallMepClash.Services
{
    public class ClashViewService
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly WallCollectorService _collector;

        public ClashViewService(UIDocument uiDoc, WallCollectorService collector)
        {
            _uiDoc = uiDoc;
            _doc = uiDoc.Document;
            _collector = collector;
        }

        public void ShowClashIn3D(int hostWallId, int linkMepId, RevitLinkInstance linkInstance)
        {
            ElementId wallId = new ElementId((long)hostWallId);
            Element wall = _doc.GetElement(wallId);
            if (wall == null)
                throw new InvalidOperationException("Host wall was not found in the active document.");

            if (linkInstance == null)
                throw new ArgumentNullException(nameof(linkInstance), "Linked model instance cannot be null.");

            Document linkDoc = linkInstance.GetLinkDocument();
            if (linkDoc == null)
                throw new InvalidOperationException("Linked model document is not loaded.");

            ElementId mepId = new ElementId((long)linkMepId);
            Element mep = linkDoc.GetElement(mepId);
            if (mep == null)
                throw new InvalidOperationException("MEP element was not found in the linked document.");

            View3D view3D = _collector.FindSuitable3DView(_uiDoc.ActiveView);
            if (view3D == null)
                throw new InvalidOperationException("No suitable 3D view was found. Create a printable 3D view and try again.");

            // 1. Get Wall BB in host coordinates
            BoundingBoxXYZ wallBB = wall.get_BoundingBox(null);
            if (wallBB == null)
                throw new InvalidOperationException("Host wall has no bounding box.");

            // 2. Get MEP BB and transform to host coordinates
            BoundingBoxXYZ mepBBLink = mep.get_BoundingBox(null);
            if (mepBBLink == null)
                throw new InvalidOperationException("MEP element has no bounding box.");

            BoundingBoxXYZ mepBBHost = BoundingBoxHelper.TransformToHost(mepBBLink, linkInstance.GetTotalTransform());

            // 3. Union Bounding Box
            BoundingBoxXYZ unionBB = GetUnionBoundingBox(wallBB, mepBBHost);

            _uiDoc.ActiveView = view3D;

            using (var transaction = new Transaction(_doc, "Show Wall-MEP clash in 3D"))
            {
                transaction.Start();

                try
                {
                    if (view3D.IsTemporaryHideIsolateActive())
                        view3D.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                }
                catch (Exception ex)
                {
                    AppLogger.Warning("[WallMepClash] Could not clear previous temporary isolate: " + ex.Message);
                }

                view3D.IsSectionBoxActive = true;
                view3D.SetSectionBox(CreateExpandedSectionBox(unionBB, 3.0)); // 3 feet margin
                
                // Chỉ isolate host wall (vì mep element ở file link không thể isolate trực tiếp bằng IsolateElementsTemporary trên view host)
                view3D.IsolateElementsTemporary(new List<ElementId> { wallId });

                transaction.Commit();
            }

            _uiDoc.Selection.SetElementIds(new List<ElementId> { wallId });
            _uiDoc.ShowElements(wallId);
        }

        private static BoundingBoxXYZ GetUnionBoundingBox(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            double minX = Math.Min(a.Min.X, b.Min.X);
            double minY = Math.Min(a.Min.Y, b.Min.Y);
            double minZ = Math.Min(a.Min.Z, b.Min.Z);

            double maxX = Math.Max(a.Max.X, b.Max.X);
            double maxY = Math.Max(a.Max.Y, b.Max.Y);
            double maxZ = Math.Max(a.Max.Z, b.Max.Z);

            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ)
            };
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
