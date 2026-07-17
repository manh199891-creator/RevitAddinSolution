using System;
using System.Collections.Generic;
using Antigravity.ArchModeling.Models;
using Autodesk.Revit.DB;

namespace Antigravity.ArchModeling.Services
{
    public class WallCenterlineStitcher
    {
        // Tolerances
        private const double CollinearAngleToleranceDeg = 1.0;
        private const double OffsetToleranceFt = 50.0 / 304.8; // ~50mm lateral offset

        // Block must be within this perpendicular distance from the wall axis to count as evidence
        private const double EvidenceAxisToleranceFt = 200.0 / 304.8; // 200mm perpendicular to wall

        // Block must land inside [gapStart - margin, gapEnd + margin] on the wall axis
        private const double EvidenceInGapMarginFt = 300.0 / 304.8; // 300mm margin each side of gap

        // Hard cap: never merge gaps larger than this without evidence (5m)
        private const double MaxGapWithoutEvidenceFt = 5000.0 / 304.8;

        public List<WallData> Stitch(List<WallData> walls, List<BlockInfo> evidenceBlocks)
        {
            var result = new List<WallData>();
            var remaining = new List<WallData>(walls);

            while (remaining.Count > 0)
            {
                var current = remaining[0];
                remaining.RemoveAt(0);

                bool merged = true;
                while (merged)
                {
                    merged = false;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        var candidate = remaining[i];
                        if (TryMerge(current, candidate, evidenceBlocks, out var mergedWall))
                        {
                            current = mergedWall;
                            remaining.RemoveAt(i);
                            merged = true;
                            break;
                        }
                    }
                }
                result.Add(current);
            }

            return result;
        }

        private bool TryMerge(WallData w1, WallData w2, List<BlockInfo> blocks, out WallData mergedWall)
        {
            mergedWall = null;
            if (!(w1.CenterLine is Line l1) || !(w2.CenterLine is Line l2)) return false;

            // 1. Check if directions are parallel (or anti-parallel)
            double angle = l1.Direction.AngleTo(l2.Direction);
            double deg = angle * 180.0 / Math.PI;
            if (deg > CollinearAngleToleranceDeg && deg < 180.0 - CollinearAngleToleranceDeg) return false;

            // 2. Check collinear: perpendicular offset of l2.Start from l1's infinite line
            XYZ v1 = l2.GetEndPoint(0) - l1.GetEndPoint(0);
            double offset = v1.CrossProduct(l1.Direction).GetLength();
            if (offset > OffsetToleranceFt) return false;

            // 3. Project all 4 endpoints onto l1's direction axis
            XYZ p1 = l1.GetEndPoint(0);
            XYZ dir = l1.Direction;

            double t1 = 0;
            double t2 = (l1.GetEndPoint(1) - p1).DotProduct(dir);
            double t3 = (l2.GetEndPoint(0) - p1).DotProduct(dir);
            double t4 = (l2.GetEndPoint(1) - p1).DotProduct(dir);

            double min1 = Math.Min(t1, t2);
            double max1 = Math.Max(t1, t2);
            double min2 = Math.Min(t3, t4);
            double max2 = Math.Max(t3, t4);

            // 4. Determine gap (>0) or overlap (<=0)
            double gapStart, gapEnd;
            double gap;
            if (max1 < min2)
            {
                gap = min2 - max1;
                gapStart = max1;
                gapEnd = min2;
            }
            else if (max2 < min1)
            {
                gap = min1 - max2;
                gapStart = max2;
                gapEnd = min1;
            }
            else
            {
                // Segments overlap — safe to merge directly (they are the SAME wall)
                gap = 0;
                gapStart = 0;
                gapEnd = 0;
            }

            if (gap > 0)
            {
                // Hard safety cap: refuse to merge across very large gaps with no evidence
                if (gap > MaxGapWithoutEvidenceFt) return false;

                // 5. Evidence check: block must be:
                //    a) Within [gapStart - margin, gapEnd + margin] on the wall axis
                //    b) Within EvidenceAxisToleranceFt perpendicular to the wall axis
                bool hasEvidence = false;
                foreach (var b in blocks)
                {
                    XYZ blockPt = Antigravity.Core.Services.CoordinateService.CadToRevit(b.X, b.Y, 0);

                    // Project block onto wall axis
                    double tBlock = (blockPt - p1).DotProduct(dir);

                    // Must be inside the gap interval (with margin)
                    if (tBlock < gapStart - EvidenceInGapMarginFt) continue;
                    if (tBlock > gapEnd + EvidenceInGapMarginFt) continue;

                    // Must be close to the wall axis (perpendicular)
                    XYZ projectedOnAxis = p1 + dir * tBlock;
                    double perpDist = blockPt.DistanceTo(projectedOnAxis);
                    if (perpDist > EvidenceAxisToleranceFt) continue;

                    hasEvidence = true;
                    break;
                }

                if (!hasEvidence) return false;
            }

            // 6. Merge segments into one long line
            double minOverall = Math.Min(min1, min2);
            double maxOverall = Math.Max(max1, max2);

            XYZ newStart = p1 + dir * minOverall;
            XYZ newEnd = p1 + dir * maxOverall;

            Line mergedLine = Line.CreateBound(newStart, newEnd);
            mergedWall = new WallData(new List<Curve>(), w1.ThicknessMm, mergedLine);
            return true;
        }
    }
}
