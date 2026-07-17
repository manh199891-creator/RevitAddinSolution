using Autodesk.Revit.DB;
using Antigravity.ZoneSplit.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.ZoneSplit.Services
{
    public sealed class PartSplitService
    {
        private readonly Document _doc;

        public PartSplitService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Tạo Parts cho các đối tượng (VD: Sàn) và cắt chúng dựa trên ranh giới của các Zone.
        /// </summary>
        public List<ElementId> CreateAndDivideParts(IEnumerable<ElementId> elementIds, IReadOnlyList<ZoneVolume> zones)
        {
            var sourceIds = elementIds?.Distinct().ToList() ?? new List<ElementId>();
            if (!sourceIds.Any() || zones == null || !zones.Any())
                return new List<ElementId>();

            // 1. Lọc các phần tử hợp lệ để tạo Parts
            var validIdsToCreateParts = new List<ElementId>();
            foreach (var id in sourceIds)
            {
                if (PartUtils.AreElementsValidForCreateParts(_doc, new List<ElementId> { id }))
                {
                    validIdsToCreateParts.Add(id);
                }
            }

            if (validIdsToCreateParts.Any())
            {

            // 2. Tạo Parts
                using (var tx = new Transaction(_doc, "ZoneSplit - Create Parts"))
                {
                    tx.Start();
                    PartUtils.CreateParts(_doc, validIdsToCreateParts);
                    _doc.Regenerate();
                    tx.Commit();
                }
            }

            // 3. Lấy danh sách các Parts vừa được tạo ra từ các phần tử gốc
            var createdPartIds = new List<ElementId>();
            foreach (var id in sourceIds)
            {
                var parts = PartUtils.GetAssociatedParts(_doc, id, includePartsWithAssociatedParts: false, includeAllChildren: true);
                createdPartIds.AddRange(parts);
            }
            createdPartIds = createdPartIds.Distinct().ToList();

            if (!createdPartIds.Any()) return createdPartIds;

            // 4. Cắt Parts (DivideParts) bang duong bao (CurveArray)
            using (var tx = new Transaction(_doc, "ZoneSplit - Divide Parts"))
            {
                tx.Start();

                // Loc cac part hop le de cat
                var partsToDivide = CollectDividableParts(createdPartIds);

                if (partsToDivide.Any())
                {
                    foreach (var zone in zones)
                    {
                        DividePartsByZone(partsToDivide, zone);
                        _doc.Regenerate();
                        partsToDivide = CollectDividableParts(CollectAssociatedParts(sourceIds));
                        if (!partsToDivide.Any()) break;
                    }
                }

                tx.Commit();
            }

            return CollectAssociatedParts(sourceIds);
        }

        private List<ElementId> CollectAssociatedParts(IEnumerable<ElementId> sourceIds)
        {
            var partIds = new List<ElementId>();
            foreach (var id in sourceIds ?? Enumerable.Empty<ElementId>())
            {
                var parts = PartUtils.GetAssociatedParts(_doc, id, includePartsWithAssociatedParts: false, includeAllChildren: true);
                partIds.AddRange(parts);
            }

            return partIds.Distinct().ToList();
        }

        private List<ElementId> CollectDividableParts(IEnumerable<ElementId> partIds)
        {
            var result = new List<ElementId>();
            foreach (var partId in partIds?.Distinct() ?? Enumerable.Empty<ElementId>())
            {
                if (PartUtils.ArePartsValidForDivide(_doc, new List<ElementId> { partId }))
                {
                    result.Add(partId);
                }
            }

            return result;
        }

        private void DividePartsByZone(List<ElementId> partsToDivide, ZoneVolume zone)
        {
            var bottomCurves = GetBottomProfile(zone.Solid);
            if (bottomCurves.Count == 0) return;

            // Tao SketchPlane nam ngang de chua cac duong cat
            var origin = bottomCurves[0].GetEndPoint(0);
            var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, origin);
            var sketchPlane = SketchPlane.Create(_doc, plane);

            try
            {
                var emptyRefPlanes = new List<ElementId>();
                PartUtils.DivideParts(_doc, partsToDivide, emptyRefPlanes, bottomCurves, sketchPlane.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DivideParts error: {ex.Message}");
            }
            finally
            {
                _doc.Delete(sketchPlane.Id);
            }
        }

        private IList<Curve> GetBottomProfile(Solid solid)
        {
            var curves = new List<Curve>();
            if (solid == null || solid.Faces.Size == 0) return curves;

            foreach (Face face in solid.Faces)
            {
                if (face is PlanarFace pf)
                {
                    // Kiem tra neu mat phang huong xuong duoi (Normal gan voi -Z)
                    if (pf.FaceNormal.IsAlmostEqualTo(-XYZ.BasisZ, 0.01))
                    {
                        foreach (CurveLoop loop in face.GetEdgesAsCurveLoops())
                        {
                            foreach (Curve curve in loop)
                            {
                                curves.Add(curve);
                            }
                        }
                    }
                }
            }

            // Neu khong tim thay mat day phang, thu lay BoundingBox lam bien dang chu nhat
            if (curves.Count == 0)
            {
                    var bbox = solid.GetBoundingBox();
                    if (TryGetWorldBounds(bbox, out var min, out var max))
                    {
                    var p0 = new XYZ(min.X, min.Y, min.Z);
                    var p1 = new XYZ(max.X, min.Y, min.Z);
                    var p2 = new XYZ(max.X, max.Y, min.Z);
                    var p3 = new XYZ(min.X, max.Y, min.Z);

                    curves.Add(Line.CreateBound(p0, p1));
                    curves.Add(Line.CreateBound(p1, p2));
                    curves.Add(Line.CreateBound(p2, p3));
                    curves.Add(Line.CreateBound(p3, p0));
                }
            }

            return curves;
        }

        private static bool TryGetWorldBounds(BoundingBoxXYZ bbox, out XYZ min, out XYZ max)
        {
            min = null;
            max = null;
            if (bbox == null) return false;

            var transform = bbox.Transform ?? Transform.Identity;
            var corners = new[]
            {
                new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Min.Z),
                new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Max.Z),
                new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z),
                new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Max.Z),
                new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z),
                new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Max.Z),
                new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Min.Z),
                new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Max.Z)
            }.Select(c => transform.OfPoint(c)).ToList();

            min = new XYZ(corners.Min(p => p.X), corners.Min(p => p.Y), corners.Min(p => p.Z));
            max = new XYZ(corners.Max(p => p.X), corners.Max(p => p.Y), corners.Max(p => p.Z));
            return true;
        }
    }
}
