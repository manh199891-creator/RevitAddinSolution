using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.Autojoin.Models;
using Antigravity.Core.Services;

namespace Antigravity.Autojoin.Services
{
    public enum JoinScope { ActiveView, Selection }

    public class FailedJoinInfo
    {
        public string ElementA { get; set; }
        public string ElementB { get; set; }
        public string Reason   { get; set; }
    }

    public class JoinResult
    {
        public int TotalIntersections => JoinCount + AlreadyCount + SkipCount;
        public int JoinCount    { get; set; }
        public int UnjoinCount  { get; set; }
        public int AlreadyCount { get; set; }
        public int SkipCount    { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<FailedJoinInfo> FailedJoins { get; set; } = new List<FailedJoinInfo>();
    }

    public enum JoinAction { Join, Unjoin }

    public static class AutoJoinService
    {
        // Cache chống vòng lặp vô tận (Ping-pong loop)
        private static Dictionary<string, DateTime> _actionCache = new Dictionary<string, DateTime>();

        // ─────────────────────────────────────────────────────────────────
        //  PUBLIC API — Pair-based
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Join phần tử theo danh sách cặp rule.
        /// Với mỗi rule: CategoryA cắt xuyên CategoryB (trừ khi SwapPriority).
        /// </summary>
        public static JoinResult ExecuteJoin(
            Document doc,
            UIDocument uidoc,
            IList<JoinRule> rules,
            JoinScope scope)
        {
            var result  = new JoinResult();
            var enabled = rules.Where(r => r.IsEnabled).ToList();
            if (enabled.Count == 0) return result;

            using (var tx = new Transaction(doc, "AutoJoin - Join Geometry"))
            {
                if (tx.Start() != TransactionStatus.Started) return result;

                try
                {
                    foreach (var rule in enabled)
                    {
                        var elemsA = GetElements(doc, uidoc, rule.CategoryA, scope);
                        var elemsB = GetElements(doc, uidoc, rule.CategoryB, scope);
                        
                        if (elemsA.Count == 0 || elemsB.Count == 0)
                        {
                            result.Errors.Add($"Rule {rule.Order}: Không tìm thấy phần tử nào (A:{elemsA.Count}, B:{elemsB.Count}) cho {rule.DisplayName}");
                            continue;
                        }

                        // A luôn cắt B
                        ProcessJoin(doc, elemsA, elemsB, result);
                    }

                    tx.Commit();
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    result.Errors.Add($"Transaction failed: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>Unjoin tất cả cặp theo rules.</summary>
        public static JoinResult ExecuteUnjoin(
            Document doc,
            UIDocument uidoc,
            IList<JoinRule> rules,
            JoinScope scope)
        {
            var result  = new JoinResult();
            var enabled = rules.Where(r => r.IsEnabled).ToList();
            if (enabled.Count == 0) return result;

            using (var tx = new Transaction(doc, "AutoJoin - Unjoin Geometry"))
            {
                if (tx.Start() != TransactionStatus.Started) return result;

                try
                {
                    foreach (var rule in enabled)
                    {
                        var elemsA = GetElements(doc, uidoc, rule.CategoryA, scope);
                        var elemsB = GetElements(doc, uidoc, rule.CategoryB, scope);
                        if (elemsA.Count == 0 || elemsB.Count == 0) continue;

                        ProcessUnjoin(doc, elemsA, elemsB, result);
                    }

                    tx.Commit();
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    result.Errors.Add($"Transaction failed: {ex.Message}");
                }
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        //  PUBLIC API — DMU (single element, no transaction)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Join một element vừa thêm/sửa với tất cả element liên quan theo rules.
        /// Gọi từ IUpdater.Execute() — KHÔNG tạo Transaction.
        /// </summary>
        public static void JoinSingleElement(
            Document doc,
            Element element,
            IList<JoinRule> rules)
        {
            if (element == null || !element.IsValidObject) return;
            if (element.Category == null) return;

            long catId = element.Category.Id.Value;

            foreach (var rule in rules)
            {
                if (!rule.IsEnabled) continue;

                // Tìm xem element thuộc CategoryA hay CategoryB của rule
                if (!TryParseBic(rule.CategoryA, out var bicA)) continue;
                if (!TryParseBic(rule.CategoryB, out var bicB)) continue;

                bool isA = catId == (long)bicA;
                bool isB = catId == (long)bicB;
                if (!isA && !isB) continue;

                // Lấy elements phía đối diện
                BuiltInCategory partnerBic = isA ? bicB : bicA;
                List<Element> partners = new List<Element>();

                try
                {
                    // Sử dụng ElementIntersectsElementFilter để tìm chính xác các đối tượng giao cắt (loại bỏ hoàn toàn BoundingBox AABB lỏng lẻo)
                    var intersectFilter = new ElementIntersectsElementFilter(element);
                    partners = new FilteredElementCollector(doc)
                        .OfCategory(partnerBic)
                        .WhereElementIsNotElementType()
                        .WherePasses(intersectFilter)
                        .Where(e => e.Id != element.Id)
                        .ToList();
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"[AutoJoin] ElementIntersectsFilter failed for {element.Id}: {ex.Message}");

                    // Fallback: Trong trường hợp phần tử vừa tạo chưa có Solid hợp lệ (hiếm gặp), dùng BoundingBox
                    var bb = element.get_BoundingBox(null);
                    if (bb != null)
                    {
                        var outline = new Outline(bb.Min - new XYZ(0.1, 0.1, 0.1), bb.Max + new XYZ(0.1, 0.1, 0.1));
                        var bbFilter = new BoundingBoxIntersectsFilter(outline);

                        partners = new FilteredElementCollector(doc)
                            .OfCategory(partnerBic)
                            .WhereElementIsNotElementType()
                            .WherePasses(bbFilter)
                            .Where(e => e.Id != element.Id)
                            .ToList();
                    }
                }

                foreach (var partner in partners)
                {
                    try
                    {
                        bool isJoined = JoinGeometryUtils.AreElementsJoined(doc, element, partner);
                        if (!isJoined)
                        {
                            string key = $"JOIN-{element.Id.Value}-{partner.Id.Value}";
                            string keyReverse = $"JOIN-{partner.Id.Value}-{element.Id.Value}";
                            
                            if (IsRecentlyAttempted(key) || IsRecentlyAttempted(keyReverse))
                                continue;

                            _actionCache[key] = DateTime.Now;
                            _actionCache[keyReverse] = DateTime.Now;

                            JoinGeometryUtils.JoinGeometry(doc, element, partner);
                        }

                        // Sau khi join (hoặc nếu đã join), đảm bảo thứ tự cắt đúng rule
                        // (Bỏ qua ép thứ tự cắt nếu 2 phần tử cùng category để tránh ping-pong)
                        if (element.Category.Id.Value != partner.Category.Id.Value)
                        {
                            var (w, l) = DetermineWinnerLoser(rule, isA, element, partner);
                            EnsureWinnerCuts(doc, w, l);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warning($"[AutoJoin] Cannot join {element.Id}-{partner.Id}: {ex.Message}");
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────

        private static (Element winner, Element loser) DetermineWinnerLoser(
            JoinRule rule, bool elementIsA, Element element, Element partner)
        {
            // A cắt B (default). 
            // A là winner
            return elementIsA
                ? (element, partner)   // element=A(winner), partner=B(loser)
                : (partner, element);  // element=B(loser), partner=A(winner)
        }

        private static void ProcessJoin(
            Document doc,
            List<Element> elemsWinner,
            List<Element> elemsLoser,
            JoinResult result)
        {
            if (elemsWinner.Count == 0 || elemsLoser.Count == 0) return;
            
            // Chuyển tập hợp Loser sang danh sách Ids để sử dụng cho FilteredElementCollector
            var loserIds = elemsLoser.Select(x => x.Id).ToList();

            foreach (var w in elemsWinner)
            {
                if (w == null || !w.IsValidObject) continue;

                List<Element> intersectingLosers;
                try
                {
                    // Lọc chính xác các Loser CÓ GIAO CẮT VẬT LÝ với Winner
                    var filter = new ElementIntersectsElementFilter(w);
                    intersectingLosers = new FilteredElementCollector(doc, loserIds)
                        .WherePasses(filter)
                        .ToList();

                    // Fallback nếu không tìm thấy bằng Solid intersection (có thể do lỗi geometry)
                    if (intersectingLosers.Count == 0)
                    {
                        var bb = w.get_BoundingBox(null);
                        if (bb != null)
                        {
                            var outline = new Outline(bb.Min, bb.Max);
                            var bbFilter = new BoundingBoxIntersectsFilter(outline);
                            intersectingLosers = new FilteredElementCollector(doc, loserIds)
                                .WherePasses(bbFilter)
                                .ToList();
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"[AutoJoin] Winner {w.Id} has no geometry: {ex.Message}");

                    // Fallback nếu Winner không có hình học để tạo filter
                    continue;
                }

                foreach (var l in intersectingLosers)
                {
                    if (l == null || !l.IsValidObject) continue;
                    if (w.Id == l.Id) continue;

                    try
                    {
                        bool sameCategory = w.Category.Id.Value == l.Category.Id.Value;
                        
                        // Nếu cùng loại (VD: Sàn với Sàn), ta chỉ xử lý 1 chiều (A cắt B hoặc B cắt A)
                        // Bằng cách ép w.Id > l.Id để lọc bớt một nửa số lần lặp bị trùng lặp.
                        if (sameCategory && w.Id.Value > l.Id.Value) continue;

                        if (JoinGeometryUtils.AreElementsJoined(doc, w, l))
                        {
                            if (!sameCategory) EnsureWinnerCuts(doc, w, l);
                            result.AlreadyCount++;
                            continue;
                        }

                        JoinGeometryUtils.JoinGeometry(doc, w, l);
                        if (!sameCategory) EnsureWinnerCuts(doc, w, l);
                        result.JoinCount++;
                    }
                    catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                    {
                        result.SkipCount++;
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentException)
                    {
                        result.SkipCount++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"[{w.Id}–{l.Id}] {ex.Message}");
                        result.FailedJoins.Add(new FailedJoinInfo
                        {
                            ElementA = $"{w.Name} [{w.Id}]",
                            ElementB = $"{l.Name} [{l.Id}]",
                            Reason   = ex.Message
                        });
                        result.SkipCount++;
                    }
                }
            }
        }

        private static void ProcessUnjoin(
            Document doc,
            List<Element> elemsA,
            List<Element> elemsB,
            JoinResult result)
        {
            if (elemsA.Count == 0 || elemsB.Count == 0) return;

            // Chuyển danh sách B thành HashSet để tra cứu O(1)
            var setB = new HashSet<ElementId>(elemsB.Select(e => e.Id));

            foreach (var a in elemsA)
            {
                if (a == null || !a.IsValidObject) continue;

                // Thay vì quét Mọi B để kiểm tra AreElementsJoined, ta lấy trực tiếp danh sách đang join với A
                ICollection<ElementId> joinedIds;
                try 
                {
                    joinedIds = JoinGeometryUtils.GetJoinedElements(doc, a);
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"[AutoJoin] GetJoinedElements failed for {a.Id}: {ex.Message}");
                    continue;
                }

                foreach (var joinedId in joinedIds)
                {
                    // Nếu phần tử đang join với A nằm trong nhóm B (setB), ta tiến hành Unjoin
                    if (setB.Contains(joinedId))
                    {
                        try
                        {
                            var b = doc.GetElement(joinedId);
                            if (b == null || !b.IsValidObject) continue;

                            JoinGeometryUtils.UnjoinGeometry(doc, a, b);
                            result.UnjoinCount++;
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Unjoin [{a.Id}–{joinedId}] {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Đảm bảo winner là phần tử cắt xuyên loser.
        /// Thêm cơ chế Time-based Lock để chống vòng lặp DMU (Ping-pong).
        /// </summary>
        private static void EnsureWinnerCuts(Document doc, Element winner, Element loser)
        {
            try
            {
                var cuttingId = JoinGeometryUtils.IsCuttingElementInJoin(doc, winner, loser);
                // Nếu winner không phải là cutting element → đảo
                if (!cuttingId)
                {
                    string key = $"SWITCH-{winner.Id.Value}-{loser.Id.Value}";
                    if (IsRecentlyAttempted(key)) return;

                    _actionCache[key] = DateTime.Now;
                    JoinGeometryUtils.SwitchJoinOrder(doc, winner, loser);
                }
            }
            catch (Exception ex)
            {
                string msg = $"SwitchJoinOrder failed [{winner.Id}–{loser.Id}]: {ex.Message}";
                AppLogger.Warning($"[AutoJoin] {msg}");
                // Không add vào result.Errors để tránh spam, chỉ log debug
            }
        }

        private static bool IsRecentlyAttempted(string key)
        {
            if (_actionCache.TryGetValue(key, out DateTime lastTime))
            {
                if ((DateTime.Now - lastTime).TotalSeconds < 2)
                    return true;
            }
            return false;
        }

        private static List<Element> GetElements(
            Document doc, UIDocument uidoc,
            string builtInCategoryName, JoinScope scope)
        {
            if (!TryParseBic(builtInCategoryName, out var bic))
                return new List<Element>();

            try
            {
                if (scope == JoinScope.Selection)
                {
                    var selectedIds = uidoc.Selection.GetElementIds();
                    return selectedIds
                        .Select(id => doc.GetElement(id))
                        .Where(e => e != null
                                 && e.IsValidObject
                                 && e.Category != null
                                 && e.Category.Id.Value == (long)bic)
                        .ToList();
                }
                else
                {
                    return new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .Where(e => e != null && e.IsValidObject)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"[AutoJoin] GetElements failed for {builtInCategoryName}: {ex.Message}");
                return new List<Element>();
            }
        }

        private static bool TryParseBic(string name, out BuiltInCategory bic)
        {
            if (string.IsNullOrEmpty(name)) { bic = BuiltInCategory.INVALID; return false; }
            
            // Thử parse trực tiếp
            if (Enum.TryParse(name, out bic)) return true;

            // Thử thêm tiền tố OST_ nếu thiếu
            if (!name.StartsWith("OST_") && Enum.TryParse("OST_" + name, out bic)) return true;

            return false;
        }
    }
}

