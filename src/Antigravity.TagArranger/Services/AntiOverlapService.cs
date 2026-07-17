using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.TagArranger.Models;
using Antigravity.TagArranger.Core;

namespace Antigravity.TagArranger.Services
{
    /// <summary>
    /// Service phát hiện và tự động giải quyết chồng lấp annotation.
    /// A0-UPGRADE: Multi-pass 2D Greedy với Force-Directed fallback (A1).
    ///
    /// Thuật toán 2 pha:
    ///   Pha 1 — Multi-pass Greedy: Với mỗi cặp annotation chồng lấp, tính Minimum
    ///             Translation Vector (MTV) 2D và đẩy annotation ra theo trục MTV nhỏ hơn.
    ///             Lặp lại tối đa MaxPasses lần cho đến khi ổn định.
    ///   Pha 2 — Force-Directed (A1): Nếu sau MaxPasses vẫn còn overlap phức tạp,
    ///             áp dụng vật lý lực đẩy/kéo trong memory để giải quyết.
    ///   Commit:  Áp dụng tất cả thay đổi vào Revit bằng 1 Transaction duy nhất.
    /// </summary>
    public static class AntiOverlapService
    {
        private const int MaxPasses = 8;
        private const int MaxForceIterations = 40;
        private const double ForceRepulsionStrength = 1.2;
        private const double ForceAttractionStrength = 0.05;
        private const double ForceDamping = 0.85;

        public static ArrangeResult Execute(Document doc, List<AnnotationBox> boxes, double minSpacingFeet)
        {
            var result = new ArrangeResult();

            if (boxes == null || boxes.Count < 2)
            {
                result.Message = "Cần ít nhất 2 annotation để xử lý chống đè.";
                return result;
            }

            var adjustable = boxes.Where(b => b.IsPositionAdjustable).ToList();
            var anchored = boxes.Where(b => !b.IsPositionAdjustable).ToList();

            result.LogMessages.Add($"[AntiOverlap A0] Total={boxes.Count}, Adjustable={adjustable.Count}, Spacing={minSpacingFeet:F4}ft");

            if (adjustable.Count < 1) return result;

            // ── Pha 1: Multi-pass 2D Greedy — tính toán hoàn toàn trong memory ──
            var workingBoxes = adjustable.Select(b => new WorkingBox(b)).ToList();
            double padding = minSpacingFeet;

            for (int pass = 0; pass < MaxPasses; pass++)
            {
                int movedThisPass = RunGreedyPass(workingBoxes, anchored, padding);
                result.LogMessages.Add($"  Pass {pass + 1}: moved {movedThisPass} annotations");
                if (movedThisPass == 0) break;
            }

            // ── Pha 2: Force-Directed fallback cho cụm overlap còn lại ──
            int remainingOverlaps = CountOverlaps(workingBoxes, anchored, padding);
            if (remainingOverlaps > 0)
            {
                result.LogMessages.Add($"  [Force-Directed] {remainingOverlaps} overlaps remain, simulating...");
                RunForceDirected(workingBoxes, anchored, padding);
            }

            // ── Commit: Áp dụng tất cả thay đổi vào Revit bằng 1 Transaction ──
            using (var tx = new Transaction(doc, "Antigravity Anti-Overlap"))
            {
                tx.Start();

                foreach (var wb in workingBoxes)
                {
                    if (!wb.HasMoved) continue;

                    XYZ newPos = new XYZ(
                        wb.HeadPosition.X,
                        wb.HeadPosition.Y,
                        wb.OriginalBox.HeadPosition.Z);

                    if (AlignService.ApplyPosition(doc, wb.OriginalBox, newPos))
                        result.MovedCount++;
                    else
                        result.SkippedCount++;

                    result.ProcessedCount++;
                }

                tx.Commit();
            }

            result.Message = $"Anti-Overlap: Đã di chuyển {result.MovedCount} annotation, bỏ qua {result.SkippedCount}.";
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // Pha 1: Multi-pass 2D Greedy
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Một pass greedy 2D: với mỗi working box, tích lũy MTV từ tất cả box chồng lấp
        /// rồi dịch chuyển. Sắp xếp xử lý box nhỏ trước (thường dễ di chuyển).
        /// </summary>
        private static int RunGreedyPass(List<WorkingBox> adjustable, List<AnnotationBox> anchored, double padding)
        {
            int movedCount = 0;
            // Xử lý box nhỏ nhất trước để ưu tiên di chuyển item ít quan trọng hơn
            var sorted = adjustable.OrderBy(b => b.Box.Width * b.Box.Height).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var curr = sorted[i];
                double totalDx = 0, totalDy = 0;
                bool hasOverlap = false;

                // Lực đẩy từ các adjustable box khác
                for (int j = 0; j < sorted.Count; j++)
                {
                    if (i == j) continue;
                    var (dx, dy, overlaps) = Compute2DMTV(curr.Box, sorted[j].Box, padding);
                    if (overlaps)
                    {
                        totalDx += dx * 0.5; // Chia đôi — cả 2 có thể nhường
                        totalDy += dy * 0.5;
                        hasOverlap = true;
                    }
                }

                // Lực đẩy từ anchored box (không thể nhường → curr chịu toàn bộ)
                foreach (var anch in anchored)
                {
                    var (dx, dy, overlaps) = Compute2DMTV(curr.Box, anch.Box, padding);
                    if (overlaps)
                    {
                        totalDx += dx;
                        totalDy += dy;
                        hasOverlap = true;
                    }
                }

                if (hasOverlap && (Math.Abs(totalDx) > 0.0001 || Math.Abs(totalDy) > 0.0001))
                {
                    curr.HeadPosition = new XY(curr.HeadPosition.X + totalDx, curr.HeadPosition.Y + totalDy);
                    curr.Box = curr.Box.Translate(totalDx, totalDy);
                    curr.HasMoved = true;
                    movedCount++;
                }
            }

            return movedCount;
        }

        /// <summary>
        /// Tính Minimum Translation Vector (MTV) để giải phóng chồng lấp giữa box A và B.
        /// Chọn trục có overlap nhỏ hơn để di chuyển ít nhất có thể (MTV algorithm).
        /// </summary>
        private static (double dx, double dy, bool overlaps) Compute2DMTV(
            BoundingBox2D a, BoundingBox2D b, double padding)
        {
            var bExpanded = b.ExpandBy(padding * 0.5);
            if (!a.Intersects(bExpanded)) return (0, 0, false);

            double overlapX = Math.Min(a.MaxX, bExpanded.MaxX) - Math.Max(a.MinX, bExpanded.MinX);
            double overlapY = Math.Min(a.MaxY, bExpanded.MaxY) - Math.Max(a.MinY, bExpanded.MinY);

            // Cộng thêm nửa padding để đảm bảo khoảng cách tối thiểu sau khi giải
            overlapX += padding * 0.5;
            overlapY += padding * 0.5;

            double dx = 0, dy = 0;

            if (overlapX < overlapY)
            {
                dx = a.CenterX < b.CenterX ? -overlapX : overlapX;
            }
            else
            {
                dy = a.CenterY < b.CenterY ? -overlapY : overlapY;
            }

            return (dx, dy, true);
        }

        // ─────────────────────────────────────────────────────────────
        // Pha 2: Force-Directed fallback (A1)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Mô phỏng vật lý Force-Directed cho các cụm overlap còn lại sau Greedy.
        /// Lực đẩy (repulsion) giữa các tag chồng nhau.
        /// Lực kéo nhẹ (attraction) về vị trí gốc để tag không trôi quá xa.
        /// Chạy hoàn toàn trong memory — không gọi Revit API.
        /// </summary>
        private static void RunForceDirected(List<WorkingBox> adjustable, List<AnnotationBox> anchored, double padding)
        {
            var anchorPositions = adjustable.ToDictionary(b => b, b => b.HeadPosition);
            var velocities = adjustable.ToDictionary(b => b, b => new XY(0, 0));

            for (int iter = 0; iter < MaxForceIterations; iter++)
            {
                bool anyChanged = false;

                foreach (var curr in adjustable)
                {
                    double fx = 0, fy = 0;

                    // Lực đẩy từ các adjustable box chồng lấp
                    foreach (var other in adjustable)
                    {
                        if (ReferenceEquals(curr, other)) continue;
                        var (dx, dy, overlaps) = Compute2DMTV(curr.Box, other.Box, padding);
                        if (overlaps)
                        {
                            fx += dx * ForceRepulsionStrength;
                            fy += dy * ForceRepulsionStrength;
                        }
                    }

                    // Lực đẩy từ anchored boxes (mạnh gấp đôi vì anchor không nhường)
                    foreach (var anch in anchored)
                    {
                        var (dx, dy, overlaps) = Compute2DMTV(curr.Box, anch.Box, padding);
                        if (overlaps)
                        {
                            fx += dx * ForceRepulsionStrength * 2.0;
                            fy += dy * ForceRepulsionStrength * 2.0;
                        }
                    }

                    // Lực kéo nhẹ về vị trí gốc (giữ tag gần element)
                    var anchor = anchorPositions[curr];
                    fx += (anchor.X - curr.HeadPosition.X) * ForceAttractionStrength;
                    fy += (anchor.Y - curr.HeadPosition.Y) * ForceAttractionStrength;

                    // Cập nhật velocity với damping để hội tụ
                    var vel = velocities[curr];
                    vel = new XY((vel.X + fx) * ForceDamping, (vel.Y + fy) * ForceDamping);
                    velocities[curr] = vel;

                    if (Math.Abs(vel.X) > 0.0001 || Math.Abs(vel.Y) > 0.0001)
                    {
                        curr.HeadPosition = new XY(curr.HeadPosition.X + vel.X, curr.HeadPosition.Y + vel.Y);
                        curr.Box = curr.Box.Translate(vel.X, vel.Y);
                        curr.HasMoved = true;
                        anyChanged = true;
                    }
                }

                if (!anyChanged) break; // Đã hội tụ
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Helper Methods
        // ─────────────────────────────────────────────────────────────

        private static int CountOverlaps(List<WorkingBox> adjustable, List<AnnotationBox> anchored, double padding)
        {
            int count = 0;
            for (int i = 0; i < adjustable.Count; i++)
            {
                for (int j = i + 1; j < adjustable.Count; j++)
                {
                    if (adjustable[i].Box.IntersectsWithPadding(adjustable[j].Box, padding)) count++;
                }
                foreach (var anch in anchored)
                {
                    if (adjustable[i].Box.IntersectsWithPadding(anch.Box, padding)) count++;
                }
            }
            return count;
        }

        // ─────────────────────────────────────────────────────────────
        // Internal Data Structures
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Wrapper để làm việc trên bản sao trong memory trước khi commit vào Revit.
        /// </summary>
        private class WorkingBox
        {
            public AnnotationBox OriginalBox { get; }
            public XY HeadPosition { get; set; }
            public BoundingBox2D Box { get; set; }
            public bool HasMoved { get; set; }

            public WorkingBox(AnnotationBox source)
            {
                OriginalBox = source;
                HeadPosition = new XY(source.HeadPosition.X, source.HeadPosition.Y);
                Box = source.Box;
                HasMoved = false;
            }
        }

        /// <summary>
        /// Vector 2D đơn giản cho tính toán trong memory, không phụ thuộc Revit XYZ.
        /// </summary>
        private struct XY
        {
            public double X, Y;
            public XY(double x, double y) { X = x; Y = y; }
        }
    }
}
