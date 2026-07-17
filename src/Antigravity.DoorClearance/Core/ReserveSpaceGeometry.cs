using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DoorClearanceBox.Core
{
    /// <summary>
    /// Creates W × 2W × H DirectShape clearance volumes for doors/windows.
    /// Formula: Width = door width (L), Depth = 2× door width, Height = door height (H)
    /// Centered at door position.
    /// </summary>
    internal static class ReserveSpaceGeometry
    {
        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Creates (or replaces) the clearance DirectShape for a single element.
        /// Must be called inside an active Revit transaction.
        /// Caller should pre-delete old shapes via BatchDeleteForElements and
        /// pre-resolve materialId via GetOrCreateMaterial for performance.
        /// </summary>
        public static DirectShape CreateOrReplace(Document doc, Element element, bool tagIfc, Constants.GeometryType geomType, Transform transform = null, ElementId cachedMaterialId = null)
        {
            double width  = DoorCollector.GetDoorWidth(element);
            double height = DoorCollector.GetDoorHeight(element);

            // Ensure minimum dimensions (fallback to defaults)
            if (width  <= 0.001) width  = UnitUtils.ConvertToInternalUnits(900,  UnitTypeId.Millimeters);
            if (height <= 0.001) height = UnitUtils.ConvertToInternalUnits(2100, UnitTypeId.Millimeters);

            // Build the solid — throws with message on failure
            Solid solid = BuildSolid(element, width, height, geomType, transform);

            // Create DirectShape under OST_GenericModel
            var ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            ds.SetShape(new GeometryObject[] { solid });

            string catName = element.Category?.Name ?? element.GetType().Name;
            string typeStr = geomType == Constants.GeometryType.Ellipse ? "Ellipse" : "Rect";
            ds.Name = $"Clearance ({typeStr}) – {catName} {element.Id.Value}";

            // Assign material (blue, 50% transparent)
            ElementId matId = cachedMaterialId ?? GetOrCreateMaterial(doc);
            var matParam = ds.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM)
                        ?? ds.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
            matParam?.Set(matId);

            // Tracking mark — lets updater and clear command find this shape
#pragma warning disable CS0618
            ds.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)
              ?.Set($"{Constants.MarkPrefix}{element.Id.Value}");
#pragma warning restore CS0618

            if (tagIfc) ApplyIfcTag(doc, ds);

            return ds;
        }

        // ── Geometry ──────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the clearance solid.
        /// Rectangle: W × 2W × H
        /// Ellipse: Elliptical cylinder with radii (W/2, W) and height H.
        /// Centered at door position.
        /// </summary>
        public static Solid BuildSolid(Element element, double width, double height, Constants.GeometryType geomType, Transform transform = null)
        {
            // ── 1. Get origin ──────────────────────────────────────────────────
            XYZ origin = null;

            if (element is FamilyInstance fi)
            {
                if (fi.Location is LocationPoint lp)
                    origin = lp.Point;

                if (origin == null)
                {
                    var bb = element.get_BoundingBox(null);
                    if (bb != null)
                        origin = new XYZ((bb.Min.X + bb.Max.X) / 2,
                                         (bb.Min.Y + bb.Max.Y) / 2,
                                          bb.Min.Z);
                }
            }
            else if (element is Wall wall)
            {
                if (wall.Location is LocationCurve lc)
                {
                    XYZ mid = lc.Curve.Evaluate(0.5, true);
                    var bb  = wall.get_BoundingBox(null);
                    origin  = new XYZ(mid.X, mid.Y, bb?.Min.Z ?? mid.Z);
                }
            }

            if (origin == null)
                throw new InvalidOperationException(
                    $"Cannot get location for {element.GetType().Name} Id={element.Id.Value}");

            // ── 2. Get directions ──────────────────────────────────────────────
            XYZ faceDir = XYZ.BasisY; // default

            if (element is FamilyInstance fi2)
            {
                XYZ f = fi2.FacingOrientation;
                XYZ fh = new XYZ(f.X, f.Y, 0);
                if (!fh.IsZeroLength())
                    faceDir = fh.Normalize();
            }
            else if (element is Wall w2)
            {
                XYZ o = w2.Orientation;
                XYZ oh = new XYZ(o.X, o.Y, 0);
                if (!oh.IsZeroLength())
                    faceDir = oh.Normalize();
            }

            XYZ handDir = new XYZ(-faceDir.Y, faceDir.X, 0);

            // ── 3. Apply link transform ────────────────────────────────────────
            if (transform != null && !transform.IsIdentity)
            {
                origin  = transform.OfPoint(origin);
                XYZ fd  = transform.OfVector(faceDir);
                XYZ hd  = transform.OfVector(handDir);
                faceDir = new XYZ(fd.X, fd.Y, 0).IsZeroLength() ? XYZ.BasisY : new XYZ(fd.X, fd.Y, 0).Normalize();
                handDir = new XYZ(hd.X, hd.Y, 0).IsZeroLength() ? XYZ.BasisX : new XYZ(hd.X, hd.Y, 0).Normalize();
            }

            // ── 4. Build Profile ───────────────────────────────────────────────
            double hw = width / 2.0;  // half width (radius X)
            double d  = width;        // half depth (radius Y)

            // Special case for Walls: Don't use 2*Length depth!
            if (element is Wall)
            {
                // Use a fixed depth (e.g. 200mm total) for curtain walls
                d = UnitUtils.ConvertToInternalUnits(100, UnitTypeId.Millimeters);
            }

            double z  = origin.Z;

            var loops = new List<CurveLoop>();

            if (geomType == Constants.GeometryType.Rectangle)
            {
                XYZ p1 = FlatZ(origin - handDir * hw - faceDir * d, z);
                XYZ p2 = FlatZ(origin + handDir * hw - faceDir * d, z);
                XYZ p3 = FlatZ(origin + handDir * hw + faceDir * d, z);
                XYZ p4 = FlatZ(origin - handDir * hw + faceDir * d, z);

                var loop = new CurveLoop();
                loop.Append(Line.CreateBound(p1, p2));
                loop.Append(Line.CreateBound(p2, p3));
                loop.Append(Line.CreateBound(p3, p4));
                loop.Append(Line.CreateBound(p4, p1));
                loops.Add(loop);
            }
            else // Ellipse
            {
                // Radius 1 = hw (width/2), Radius 2 = d (width)
                // Revit CurveLoop cannot consist of a single closed curve. 
                // We must split it into two arcs.
                try 
                {
                    var arc1 = Ellipse.CreateCurve(origin, hw, d, handDir, faceDir, 0, Math.PI);
                    var arc2 = Ellipse.CreateCurve(origin, hw, d, handDir, faceDir, Math.PI, 2 * Math.PI);
                    
                    var loop = new CurveLoop();
                    loop.Append(arc1);
                    loop.Append(arc2);
                    loops.Add(loop);
                }
                catch (Exception)
                {
                    // Fallback to box if ellipse creation fails
                    return BuildSolid(element, width, height, Constants.GeometryType.Rectangle, transform);
                }
            }

            // ── 5. Build extrusion ────────────────────────────────────────────
            Solid rawSolid = GeometryCreationUtilities.CreateExtrusionGeometry(
                loops,
                XYZ.BasisZ,
                height);

            // ── 6. Subtract Wall Intersection ──────────────────────────────────
            // Subtraction via BooleanOperationsUtils on 600+ elements causes Revit to hang/deadlock.
            // Returning the raw solid ensures extreme performance and stability.
            return rawSolid;
        }

        private static Solid SubtractHostWall(Element element, Solid clearanceSolid, Transform transform)
        {
            try
            {
                HostObject hostObj = null;
                if (element is FamilyInstance fi && fi.Host is HostObject ho) hostObj = ho;
                else if (element is HostObject ho2) hostObj = ho2;

                if (hostObj == null) return clearanceSolid;

                // Get host geometry
                var opt = new Options { DetailLevel = ViewDetailLevel.Medium };
                var hostGeom = hostObj.get_Geometry(opt);
                if (hostGeom == null) return clearanceSolid;

                var hostSolids = new List<Solid>();
                foreach (var obj in hostGeom)
                {
                    if (obj is Solid s && s.Volume > 0) hostSolids.Add(s);
                    else if (obj is GeometryInstance gi)
                    {
                        var instGeom = gi.GetInstanceGeometry();
                        foreach (var iobj in instGeom)
                            if (iobj is Solid isol && isol.Volume > 0) hostSolids.Add(isol);
                    }
                }

                if (hostSolids.Count == 0) return clearanceSolid;

                // Transform host solids to local space if from link
                if (transform != null && !transform.IsIdentity)
                {
                    for (int i = 0; i < hostSolids.Count; i++)
                    {
                        hostSolids[i] = SolidUtils.CreateTransformed(hostSolids[i], transform);
                    }
                }

                Solid current = clearanceSolid;
                foreach (var hs in hostSolids)
                {
                    try
                    {
                        // Subtract host wall from clearance box
                        Solid result = BooleanOperationsUtils.ExecuteBooleanOperation(
                            current, hs, BooleanOperationsType.Difference);
                        if (result != null && result.Volume > 0)
                            current = result;
                    }
                    catch { /* skip problematic parts */ }
                }

                return current;
            }
            catch
            {
                return clearanceSolid;
            }
        }

        // ── Material ──────────────────────────────────────────────────────────

        public static ElementId GetOrCreateMaterial(Document doc)
        {
            const string matName = "Door Clearance Volume";

            var mat = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .OfType<Material>()
                .FirstOrDefault(m => m.Name == matName);

            if (mat != null)
                return mat.Id;

            ElementId id = Material.Create(doc, matName);
            mat = doc.GetElement(id) as Material;
            if (mat != null)
            {
                mat.Color        = new Color(0, 120, 215); // blue
                mat.Transparency = 50;
                mat.Shininess    = 0;
            }
            return id;
        }

        // ── Tracking Helpers ─────────────────────────────────────────────────

        public static DirectShape FindForElement(Document doc, ElementId elemId)
        {
#pragma warning disable CS0618
            string expectedMark = $"{Constants.MarkPrefix}{elemId.Value}";
#pragma warning restore CS0618
            return new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .OfType<DirectShape>()
                .FirstOrDefault(ds =>
                    ds.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)
                      ?.AsString() == expectedMark);
        }

        public static IDictionary<ElementId, DirectShape> FindForElements(Document doc, IEnumerable<ElementId> elemIds)
        {
            var result = new Dictionary<ElementId, DirectShape>();
            if (elemIds == null)
                return result;

#pragma warning disable CS0618
            var idsByMark = elemIds
                .Where(id => id != null)
                .Distinct()
                .ToDictionary(id => $"{Constants.MarkPrefix}{id.Value}", id => id);
#pragma warning restore CS0618

            if (idsByMark.Count == 0)
                return result;

            foreach (var ds in new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .OfType<DirectShape>())
            {
                string mark = ds.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
                if (mark != null && idsByMark.TryGetValue(mark, out ElementId elemId))
                    result[elemId] = ds;
            }

            return result;
        }

        // Keep old name for backward compat with DoorChangeUpdater
        public static DirectShape FindForDoor(Document doc, ElementId doorId)
            => FindForElement(doc, doorId);

        public static void MarkAsStale(DirectShape ds)
        {
            ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
              ?.Set(Constants.StaleFlag);
        }

        public static int DeleteAll(Document doc)
        {
            var ids = new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .OfType<DirectShape>()
                .Where(IsClearanceShape)
                .Select(ds => ds.Id)
                .ToList();
            if (ids.Count > 0)
                doc.Delete(ids);

            return ids.Count;
        }

        public static IList<DirectShape> GetAll(Document doc) =>
            new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .OfType<DirectShape>()
                .Where(IsClearanceShape)
                .ToList();

        // ── IFC ──────────────────────────────────────────────────────────────

        private static void ApplyIfcTag(Document doc, DirectShape ds)
        {
            var ifcParam = ds.LookupParameter(Constants.IfcExportAsParam);
            if (ifcParam?.StorageType == StorageType.String)
            {
                ifcParam.Set(Constants.IfcExportAsValue);
                return;
            }
            var comments = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments != null)
            {
                string cur = comments.AsString() ?? string.Empty;
                string tag = $"IFC:{Constants.IfcExportAsValue}";
                if (!cur.Contains(tag))
                    comments.Set(string.IsNullOrEmpty(cur) ? tag : $"{cur} | {tag}");
            }
        }

        // ── Private Helpers ──────────────────────────────────────────────────

        private static void DeleteForElement(Document doc, ElementId elemId)
        {
            var existing = FindForElement(doc, elemId);
            if (existing != null) doc.Delete(existing.Id);
        }

        /// <summary>
        /// Batch-deletes all existing clearance shapes for the given element IDs.
        /// Performs only ONE DB scan instead of N scans. Must be called inside a transaction.
        /// </summary>
        public static int BatchDeleteForElements(Document doc, IEnumerable<ElementId> elemIds)
        {
            var existing = FindForElements(doc, elemIds);
            if (existing.Count == 0) return 0;

            var idsToDelete = existing.Values.Select(ds => ds.Id).ToList();
            doc.Delete(idsToDelete);
            return idsToDelete.Count;
        }

        private static bool IsClearanceShape(DirectShape ds) =>
            ds.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)
              ?.AsString()?.StartsWith(Constants.MarkPrefix) == true;

        private static XYZ FlatZ(XYZ pt, double z) => new XYZ(pt.X, pt.Y, z);
    }
}
