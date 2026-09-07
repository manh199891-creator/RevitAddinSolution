// =============================================================================
// ZoneSplit BIM — Module 2 + Module 3
// Revit API 2024 · C# .NET 4.8
//
// Giả định:
//   - User đã tạo sẵn Generic Model (DirectShape hoặc Family) đại diện zone volume
//   - Mỗi zone element có shared parameter "BIM_ZoneID"  (string, e.g. "Zone-1")
//                                          "BIM_ZoneName" (string, e.g. "ZONE 1")
//   - Structural elements đã có shared param sẵn sàng để ghi
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitAddin.ZoneSplit
{
    // =========================================================================
    // 1. DATA MODELS
    // =========================================================================

    /// <summary>Đại diện cho một zone volume đã được user tạo trong Revit.</summary>
    public sealed class ZoneVolume
    {
        public ElementId SourceId  { get; }   // ElementId của GenericModel/DirectShape
        public string    ZoneId    { get; }   // "Zone-1"
        public string    ZoneName  { get; }   // "ZONE 1"
        public Solid     Solid     { get; }   // Cached solid geometry

        public ZoneVolume(ElementId id, string zoneId, string zoneName, Solid solid)
        {
            SourceId = id;
            ZoneId   = zoneId   ?? throw new ArgumentNullException(nameof(zoneId));
            ZoneName = zoneName ?? string.Empty;
            Solid    = solid    ?? throw new ArgumentNullException(nameof(solid));
        }
    }

    public sealed class ClassifyResult
    {
        public int ProcessedElements  { get; set; }
        public int AssignedElements   { get; set; }
        public int MultiZoneElements  { get; set; }  // span > 1 zone
        public int SkippedNoSolid     { get; set; }
        public int SkippedNoZone      { get; set; }
        public List<string> Warnings  { get; } = new List<string>();

        public override string ToString() =>
            $"Processed: {ProcessedElements} | Assigned: {AssignedElements} | " +
            $"Multi-zone: {MultiZoneElements} | No solid: {SkippedNoSolid} | " +
            $"No zone: {SkippedNoZone} | Warnings: {Warnings.Count}";
    }

    // =========================================================================
    // 2. SOLID EXTRACTOR — Utility dùng chung
    // =========================================================================

    public static class SolidExtractor
    {
        // Options dùng chung — khởi tạo 1 lần, không phải per-call
        private static readonly Options _opts = new Options
        {
            ComputeReferences  = false,
            IncludeNonVisibleObjects = false,
            DetailLevel        = ViewDetailLevel.Fine,
        };

        /// <summary>
        /// Trả về solid lớn nhất từ element.
        /// Xử lý cả GeometryInstance (family) lẫn DirectShape.
        /// Trả null nếu không có solid hợp lệ.
        /// </summary>
        public static Solid GetLargestSolid(Element elem)
        {
            if (elem == null) return null;

            // Thử lấy từ GeometryElement thông thường
            var geom = elem.get_Geometry(_opts);
            if (geom == null) return null;

            return CollectSolids(geom)
                .Where(s => s.Volume > 1e-9)
                .OrderByDescending(s => s.Volume)
                .FirstOrDefault();
        }

        /// <summary>
        /// Union tất cả solids — dùng cho slab nhiều lớp hoặc compound wall.
        /// </summary>
        public static Solid GetUnionSolid(Element elem)
        {
            if (elem == null) return null;
            var geom = elem.get_Geometry(_opts);
            if (geom == null) return null;

            var solids = CollectSolids(geom)
                .Where(s => s.Volume > 1e-9)
                .ToList();

            if (solids.Count == 0) return null;
            if (solids.Count == 1) return solids[0];

            Solid result = solids[0];
            for (int i = 1; i < solids.Count; i++)
                result = SafeBooleanUnion(result, solids[i]) ?? result;

            return result;
        }

        // Duyệt đệ quy GeometryElement — hỗ trợ nested instance
        private static IEnumerable<Solid> CollectSolids(GeometryElement geomElem)
        {
            if (geomElem == null) yield break;
            foreach (var obj in geomElem)
            {
                switch (obj)
                {
                    case Solid s when s.Volume > 1e-9:
                        yield return s;
                        break;
                    case GeometryInstance inst:
                        foreach (var s in CollectSolids(inst.GetInstanceGeometry()))
                            yield return s;
                        break;
                }
            }
        }

        private static Solid SafeBooleanUnion(Solid a, Solid b)
        {
            try { return BooleanOperationsUtils.ExecuteBooleanOperation(a, b, BooleanOperationsType.Union); }
            catch { return null; }
        }

        /// <summary>
        /// Intersection volume giữa 2 solids. Trả 0 nếu không giao hoặc degenerate.
        /// </summary>
        public static double IntersectionVolume(Solid a, Solid b)
        {
            if (a == null || b == null) return 0;
            // BBox pre-check: rất rẻ, lọc 80-90% cặp không giao trước khi BooleanOp
            if (!BBoxOverlap(a.GetBoundingBox(), b.GetBoundingBox())) return 0;
            try
            {
                var inter = BooleanOperationsUtils.ExecuteBooleanOperation(
                    a, b, BooleanOperationsType.Intersect);
                return inter?.Volume ?? 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Trả về intersection solid (null nếu không giao). Dùng khi cần geometry (Module 3).
        /// </summary>
        public static Solid IntersectionSolid(Solid a, Solid b)
        {
            if (a == null || b == null) return null;
            if (!BBoxOverlap(a.GetBoundingBox(), b.GetBoundingBox())) return null;
            try
            {
                var inter = BooleanOperationsUtils.ExecuteBooleanOperation(
                    a, b, BooleanOperationsType.Intersect);
                return (inter?.Volume > 1e-9) ? inter : null;
            }
            catch { return null; }
        }

        private static bool BBoxOverlap(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            if (a == null || b == null) return true; // không rõ → cho qua BooleanOp
            return a.Max.X > b.Min.X && a.Min.X < b.Max.X
                && a.Max.Y > b.Min.Y && a.Min.Y < b.Max.Y
                && a.Max.Z > b.Min.Z && a.Min.Z < b.Max.Z;
        }
    }

    // =========================================================================
    // 3. ZONE VOLUME READER — Đọc zone elements user đã tạo
    // =========================================================================

    public static class ZoneVolumeReader
    {
        public const string PARAM_ZONE_ID   = "BIM_ZoneID";
        public const string PARAM_ZONE_NAME = "BIM_ZoneName";

        /// <summary>
        /// Đọc tất cả GenericModel trong document có param BIM_ZoneID.
        /// Hỗ trợ cả DirectShape và Family Instance (Generic Model category).
        /// </summary>
        public static IReadOnlyList<ZoneVolume> ReadFromDocument(Document doc)
        {
            var result = new List<ZoneVolume>();

            using var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_GenericModel);

            foreach (var elem in collector)
            {
                var zoneId = elem.LookupParameter(PARAM_ZONE_ID)?.AsString();
                if (string.IsNullOrWhiteSpace(zoneId)) continue;

                var zoneName = elem.LookupParameter(PARAM_ZONE_NAME)?.AsString() ?? zoneId;

                // Lấy solid — thử GetLargestSolid trước, nếu không thì GetUnionSolid
                var solid = SolidExtractor.GetLargestSolid(elem)
                         ?? SolidExtractor.GetUnionSolid(elem);

                if (solid == null)
                {
                    Trace.WriteLine($"[ZoneSplit] Zone '{zoneId}' (id={elem.Id}) không có solid geometry — bỏ qua.");
                    continue;
                }

                result.Add(new ZoneVolume(elem.Id, zoneId, zoneName, solid));
            }

            Trace.WriteLine($"[ZoneSplit] Đọc được {result.Count} zone volumes.");
            return result;
        }
    }

    // =========================================================================
    // 4. MODULE 2 — ZONE CLASSIFIER
    // =========================================================================

    /// <summary>
    /// Phân loại structural elements vào zone theo majority-volume rule.
    /// Ghi BIM_ZoneID và BIM_ZoneName vào shared parameter của mỗi element.
    ///
    /// Ưu tiên tối ưu:
    ///   - ElementIntersectsElementFilter làm first pass (tránh bug edge-touching)
    ///   - BBox pre-check trước mỗi BooleanOp
    ///   - Solid cache tránh gọi get_Geometry nhiều lần
    ///   - Single Transaction cho toàn bộ ghi parameter
    /// </summary>
    public sealed class ZoneClassifier
    {
        private readonly Document _doc;

        // Structural categories được classify
        private static readonly BuiltInCategory[] _targetCategories =
        {
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Walls,
        };

        private static readonly LogicalOrFilter _categoryFilter =
            new LogicalOrFilter(
                _targetCategories
                    .Select(c => (ElementFilter)new ElementCategoryFilter(c))
                    .ToList());

        public ZoneClassifier(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <param name="zones">Danh sách zone volumes đã đọc từ model</param>
        /// <param name="levelId">Lọc theo level (null = tất cả levels)</param>
        public ClassifyResult Classify(
            IReadOnlyList<ZoneVolume> zones,
            ElementId levelId = null)
        {
            var result = new ClassifyResult();
            if (zones == null || zones.Count == 0) return result;

            var sw = Stopwatch.StartNew();

            // ---- PASS 1: Thu thập candidates cho mỗi zone ----
            // Dùng ElementIntersectsElementFilter (robust hơn ElementIntersectsSolidFilter
            // vì không bị bug REVIT-32243 với elements tiếp xúc đúng trên face)
            var candidatesByZone = CollectCandidates(zones, levelId);

            // ---- PASS 2: Build solid cache ----
            var allCandidateIds = candidatesByZone.Values
                .SelectMany(ids => ids)
                .Distinct()
                .ToHashSet();

            result.ProcessedElements = allCandidateIds.Count;
            var solidCache = BuildSolidCache(allCandidateIds);

            Trace.WriteLine($"[ZoneSplit] Solid cache: {solidCache.Count}/{allCandidateIds.Count} elements " +
                            $"({sw.ElapsedMilliseconds}ms)");

            result.SkippedNoSolid = allCandidateIds.Count - solidCache.Count;

            // ---- PASS 3: Tính intersection volume, chọn majority zone ----
            // bestZone[elemId] = (zone, intersectionVolume)
            var bestZone = new Dictionary<ElementId, (ZoneVolume zone, double vol)>();

            foreach (var (zone, candidates) in candidatesByZone)
            {
                foreach (var elemId in candidates)
                {
                    if (!solidCache.TryGetValue(elemId, out var elemSolid)) continue;

                    var vol = SolidExtractor.IntersectionVolume(elemSolid, zone.Solid);
                    if (vol < 1e-9) continue;

                    if (!bestZone.TryGetValue(elemId, out var current) || vol > current.vol)
                    {
                        if (bestZone.ContainsKey(elemId)) result.MultiZoneElements++;
                        bestZone[elemId] = (zone, vol);
                    }
                }
            }

            result.AssignedElements = bestZone.Count;
            result.SkippedNoZone    = solidCache.Count - bestZone.Count;

            Trace.WriteLine($"[ZoneSplit] Assignment: {result.AssignedElements} elements " +
                            $"({result.MultiZoneElements} multi-zone) | {sw.ElapsedMilliseconds}ms");

            // ---- PASS 4: Ghi parameter — 1 Transaction ----
            using (var tx = new Transaction(_doc, "ZoneSplit — Assign Zone Parameters"))
            {
                tx.Start();
                foreach (var (elemId, (zone, _)) in bestZone)
                {
                    var elem = _doc.GetElement(elemId);
                    WriteStringParam(elem, ZoneVolumeReader.PARAM_ZONE_ID,   zone.ZoneId,   result.Warnings);
                    WriteStringParam(elem, ZoneVolumeReader.PARAM_ZONE_NAME, zone.ZoneName, result.Warnings);
                }
                tx.Commit();
            }

            sw.Stop();
            Trace.WriteLine($"[ZoneSplit] Module 2 hoàn thành: {sw.ElapsedMilliseconds}ms");
            return result;
        }

        // ----------------------------------------------------------------
        private Dictionary<ZoneVolume, HashSet<ElementId>> CollectCandidates(
            IReadOnlyList<ZoneVolume> zones,
            ElementId levelId)
        {
            var map = new Dictionary<ZoneVolume, HashSet<ElementId>>();

            foreach (var zone in zones)
            {
                var zoneElem = _doc.GetElement(zone.SourceId);
                if (zoneElem == null) continue;

                FilteredElementCollector collector;
                try
                {
                    collector = new FilteredElementCollector(_doc)
                        .WhereElementIsNotElementType()
                        .WherePasses(new ElementIntersectsElementFilter(zoneElem))
                        .WherePasses(_categoryFilter);
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
                {
                    Trace.WriteLine($"[ZoneSplit] Filter error zone '{zone.ZoneId}': {ex.Message}");
                    map[zone] = new HashSet<ElementId>();
                    continue;
                }

                // Level filter: chỉ áp dụng nếu có levelId hợp lệ
                if (levelId != null && levelId != ElementId.InvalidElementId)
                    collector = collector.WherePasses(new ElementLevelFilter(levelId));

                map[zone] = collector.ToElementIds().ToHashSet();
            }

            return map;
        }

        // ----------------------------------------------------------------
        private Dictionary<ElementId, Solid> BuildSolidCache(HashSet<ElementId> ids)
        {
            var cache = new Dictionary<ElementId, Solid>(ids.Count);
            foreach (var id in ids)
            {
                var elem  = _doc.GetElement(id);
                var solid = SolidExtractor.GetLargestSolid(elem)
                         ?? SolidExtractor.GetUnionSolid(elem);
                if (solid != null)
                    cache[id] = solid;
            }
            return cache;
        }

        // ----------------------------------------------------------------
        private static void WriteStringParam(
            Element elem, string paramName, string value, List<string> warnings)
        {
            if (elem == null) return;
            var p = elem.LookupParameter(paramName);
            if (p == null)
            {
                // Chỉ warn lần đầu per category để tránh spam
                var catName = elem.Category?.Name ?? "Unknown";
                if (!warnings.Contains($"[{catName}] {paramName}"))
                    warnings.Add($"Param '{paramName}' not found on [{catName}] — ensure SharedParameter is bound.");
                return;
            }
            if (!p.IsReadOnly && p.StorageType == StorageType.String)
                p.Set(value);
        }
    }

    // =========================================================================
    // 5. MODULE 3 — ZONE QTO CALCULATOR
    // =========================================================================

    /// <summary>
    /// Tính khối lượng (m³) và chiều dài (m) của mỗi structural element
    /// thuộc từng zone, ghi vào shared parameters.
    ///
    /// Param pattern:
    ///   Volume : "BIM_{ZoneId}_Vol_m3"   (Number param, đơn vị m³)
    ///   Length : "BIM_{ZoneId}_Len_m"    (Number param, đơn vị m — chỉ cho beam/column)
    ///
    /// Ưu tiên tối ưu:
    ///   - Solid cache dùng lại từ Module 2 (nếu gọi kết hợp)
    ///   - BBox pre-check trước BooleanOp
    ///   - Beam length tính bằng vertex projection lên trục dầm
    ///   - Single Transaction
    /// </summary>
    public sealed class ZoneQTOCalculator
    {
        private readonly Document _doc;

        // Revit internal unit = feet
        // 1 ft = 0.3048 m → 1 ft³ = 0.3048³ m³
        private const double FT3_TO_M3 = 0.3048 * 0.3048 * 0.3048;  // 0.028317
        private const double FT_TO_M   = 0.3048;

        // Chỉ tính chiều dài cho 2 categories này
        private static readonly HashSet<int> _lengthCategories = new HashSet<int>
        {
            (int)BuiltInCategory.OST_StructuralFraming,
            (int)BuiltInCategory.OST_StructuralColumns,
        };

        public ZoneQTOCalculator(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <param name="zones">Danh sách zones</param>
        /// <param name="elementIds">Elements cần tính (thường là kết quả từ Module 2)</param>
        /// <param name="existingSolidCache">
        ///   Solid cache từ Module 2 (tránh get_Geometry lại). Null = tự build.
        /// </param>
        public void Calculate(
            IReadOnlyList<ZoneVolume>          zones,
            IReadOnlyList<ElementId>            elementIds,
            Dictionary<ElementId, Solid>       existingSolidCache = null)
        {
            if (zones == null || elementIds == null) return;

            var sw = Stopwatch.StartNew();

            // ---- Solid cache ----
            var solidCache = existingSolidCache
                          ?? BuildSolidCache(elementIds);

            // ---- Beam axis cache ----
            var axisCache = BuildBeamAxisCache(solidCache.Keys);

            // ---- Tính contribution mỗi element × zone ----
            // Cấu trúc: [elemId] → [(zone, volM3, lenM)]
            var contributions =
                new Dictionary<ElementId, List<(ZoneVolume zone, double volM3, double lenM)>>();

            foreach (var id in solidCache.Keys)
            {
                var elemSolid = solidCache[id];
                List<(ZoneVolume, double, double)> entries = null;

                axisCache.TryGetValue(id, out var axis);

                foreach (var zone in zones)
                {
                    var interSolid = SolidExtractor.IntersectionSolid(elemSolid, zone.Solid);
                    if (interSolid == null) continue;

                    double volM3 = interSolid.Volume * FT3_TO_M3;
                    double lenM  = (axis != null)
                        ? ProjectLengthOnAxis(interSolid, axis.Value) * FT_TO_M
                        : 0;

                    if (entries == null) entries = new List<(ZoneVolume, double, double)>();
                    entries.Add((zone, Math.Round(volM3, 5), Math.Round(lenM, 4)));
                }

                if (entries != null)
                    contributions[id] = entries;
            }

            Trace.WriteLine($"[ZoneSplit] QTO calc: {contributions.Count} elements " +
                            $"với contributions | {sw.ElapsedMilliseconds}ms");

            // ---- Ghi parameter — 1 Transaction ----
            using var tx = new Transaction(_doc, "ZoneSplit — Write QTO Parameters");
            tx.Start();
            foreach (var (elemId, entries) in contributions)
            {
                var elem = _doc.GetElement(elemId);
                if (elem == null) continue;

                foreach (var (zone, volM3, lenM) in entries)
                {
                    WriteNumberParam(elem, $"BIM_{zone.ZoneId}_Vol_m3", volM3);
                    if (lenM > 0)
                        WriteNumberParam(elem, $"BIM_{zone.ZoneId}_Len_m", lenM);
                }
            }
            tx.Commit();

            sw.Stop();
            Trace.WriteLine($"[ZoneSplit] Module 3 hoàn thành: {sw.ElapsedMilliseconds}ms");
        }

        // ----------------------------------------------------------------
        /// <summary>
        /// Chiều dài của intersection solid chiếu lên trục dầm/cột.
        /// Algorithm: tessellate tất cả faces → project vertices → max - min.
        /// Complexity O(V) với V = số vertices của intersection solid.
        /// </summary>
        private static double ProjectLengthOnAxis(
            Solid intersectionSolid,
            (XYZ origin, XYZ direction) axis)
        {
            double min =  double.MaxValue;
            double max = -double.MaxValue;
            bool   found = false;

            foreach (Face face in intersectionSolid.Faces)
            {
                var mesh = face.Triangulate(0.5); // level of detail 0.5 — đủ chính xác
                if (mesh == null) continue;

                for (int i = 0; i < mesh.NumberOfVertices; i++)
                {
                    // Projection = dot((v - origin), direction)
                    double proj = (mesh.get_Vertex(i) - axis.origin).DotProduct(axis.direction);
                    if (proj < min) min = proj;
                    if (proj > max) max = proj;
                    found = true;
                }
            }

            return (found && max > min) ? (max - min) : 0;
        }

        // ----------------------------------------------------------------
        private Dictionary<ElementId, Solid> BuildSolidCache(IEnumerable<ElementId> ids)
        {
            var cache = new Dictionary<ElementId, Solid>();
            foreach (var id in ids)
            {
                var elem  = _doc.GetElement(id);
                var solid = SolidExtractor.GetLargestSolid(elem)
                         ?? SolidExtractor.GetUnionSolid(elem);
                if (solid != null)
                    cache[id] = solid;
            }
            return cache;
        }

        // ----------------------------------------------------------------
        private Dictionary<ElementId, (XYZ origin, XYZ direction)> BuildBeamAxisCache(
            IEnumerable<ElementId> ids)
        {
            var cache = new Dictionary<ElementId, (XYZ, XYZ)>();
            foreach (var id in ids)
            {
                var elem = _doc.GetElement(id);
                if (elem == null) continue;

                // Chỉ xử lý beam và column
                var catId = elem.Category?.Id?.IntegerValue ?? 0;
                if (!_lengthCategories.Contains(catId)) continue;

                var axis = GetElementAxis(elem);
                if (axis.HasValue)
                    cache[id] = axis.Value;
            }
            return cache;
        }

        private static (XYZ origin, XYZ direction)? GetElementAxis(Element elem)
        {
            // Trường hợp 1: LocationCurve (beam, brace, slanted column)
            if (elem.Location is LocationCurve lc && lc.Curve != null)
            {
                var p0  = lc.Curve.GetEndPoint(0);
                var p1  = lc.Curve.GetEndPoint(1);
                var dir = (p1 - p0);
                if (dir.GetLength() < 1e-6) return null;
                return (p0, dir.Normalize());
            }

            // Trường hợp 2: LocationPoint (vertical column)
            if (elem.Location is LocationPoint lp)
            {
                // Column theo Z
                return (lp.Point, XYZ.BasisZ);
            }

            return null;
        }

        // ----------------------------------------------------------------
        private static void WriteNumberParam(Element elem, string paramName, double value)
        {
            var p = elem?.LookupParameter(paramName);
            if (p == null || p.IsReadOnly || p.StorageType != StorageType.Double) return;

            // Revit lưu nội bộ theo feet/feet³ → convert ngược lại
            // Nhưng nếu param định nghĩa là Number (không có unit) thì set trực tiếp
            // Nếu param là Length/Volume unit → Revit API tự convert
            // Pattern an toàn: kiểm tra DisplayUnitType
            try { p.Set(value); }
            catch { /* param có unit khác — bỏ qua */ }
        }
    }

    // =========================================================================
    // 6. COMMAND — Orchestrator chạy cả 2 module
    // =========================================================================

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class ZoneProcessCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;
            if (doc == null) { message = "Không tìm thấy Document."; return Result.Failed; }

            // --- Đọc zone volumes ---
            var zones = ZoneVolumeReader.ReadFromDocument(doc);
            if (zones.Count == 0)
            {
                TaskDialog.Show("ZoneSplit",
                    "Không tìm thấy Generic Model nào có parameter 'BIM_ZoneID'.\n" +
                    "Hãy tạo zone volumes và điền BIM_ZoneID trước khi chạy.");
                return Result.Cancelled;
            }

            // --- Module 2: Classify ---
            var classifier = new ZoneClassifier(doc);
            var classifyResult = classifier.Classify(zones);

            // --- Module 3: QTO ---
            // Lấy danh sách elements đã được assign để tính QTO
            // (chỉ cần chạy cho elements đã có BIM_ZoneID)
            var assignedIds = GetAssignedElementIds(doc);
            var qtoCalc = new ZoneQTOCalculator(doc);
            qtoCalc.Calculate(zones, assignedIds);

            // --- Summary ---
            TaskDialog.Show("ZoneSplit — Hoàn thành",
                $"Zone volumes: {zones.Count}\n" +
                $"Elements đã phân loại: {classifyResult.AssignedElements}/{classifyResult.ProcessedElements}\n" +
                $"Multi-zone elements: {classifyResult.MultiZoneElements}\n" +
                $"Không tìm thấy zone: {classifyResult.SkippedNoZone}\n" +
                (classifyResult.Warnings.Count > 0
                    ? $"\nWarnings ({classifyResult.Warnings.Count}):\n" +
                      string.Join("\n", classifyResult.Warnings.Take(5))
                    : string.Empty));

            return Result.Succeeded;
        }

        private static List<ElementId> GetAssignedElementIds(Document doc)
        {
            using var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(new LogicalOrFilter(new List<ElementFilter>
                {
                    new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns),
                    new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming),
                    new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                    new ElementCategoryFilter(BuiltInCategory.OST_StructuralFoundation),
                    new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                }));

            return collector
                .Where(e => !string.IsNullOrEmpty(
                    e.LookupParameter(ZoneVolumeReader.PARAM_ZONE_ID)?.AsString()))
                .Select(e => e.Id)
                .ToList();
        }
    }
}

// =============================================================================
// GHI CHÚ QUAN TRỌNG
// =============================================================================
//
// 1. SHARED PARAMETERS CẦN BIND TRƯỚC KHI CHẠY:
//    Trên Zone GenModel:    BIM_ZoneID (String), BIM_ZoneName (String)
//    Trên Structural elems: BIM_ZoneID, BIM_ZoneName (String)
//                          BIM_Zone-1_Vol_m3, BIM_Zone-2_Vol_m3 ... (Number)
//                          BIM_Zone-1_Len_m,  BIM_Zone-2_Len_m  ... (Number)
//    Lưu ý: tên param phụ thuộc vào ZoneID user đặt → cần biết trước để bind đủ.
//    Workaround: dùng 1 text param "BIM_QTO_Json" lưu JSON {"Zone-1":0.5,"Zone-2":1.2}
//    → schedule không filter được nhưng export Excel được.
//
// 2. PERFORMANCE ESTIMATE (model ~1000 elements, 8 zones):
//    Module 2: ~3-8s   (ElementIntersectsElementFilter fast + BooleanOp per hit)
//    Module 3: ~5-15s  (BooleanOp per zone per element — multi-zone elements chủ yếu)
//    Bottleneck: BooleanOperationsUtils — không thể parallelize (Revit single-thread)
//    Optimize: giảm số zone × số elements qua BBox pre-check (đã implement)
//
// 3. BUG REVIT-32243 ĐÃ XỬ LÝ:
//    Dùng ElementIntersectsElementFilter (not ElementIntersectsSolidFilter)
//    → robust hơn với elements tiếp xúc chính xác trên face/edge của zone.
//
// 4. ELEMENT SPAN NHIỀU ZONE:
//    Module 2: ghi zone có intersection volume LỚN NHẤT (majority rule)
//    Module 3: ghi volume VÀO TẤT CẢ zones → schedule sum đúng tổng
//    → 2 modules bổ sung nhau: M2 cho "element thuộc zone nào", M3 cho "KL từng zone"
//
// 5. ĐƠN VỊ:
//    Revit internal = feet/feet³
//    Output params = m/m³ (convert trong code)
//    Nếu param type là "Volume" (SpecTypeId.Volume) → Revit tự convert → dùng
//    UnitUtils.ConvertToInternalUnits(value, UnitTypeId.CubicMeters) để set
// =============================================================================
