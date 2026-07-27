using Antigravity.DrawBeams.Models;
using System;

namespace Antigravity.DrawBeams.Services
{
    public static class BeamPhysicalEnvelope
    {
        public static bool IsEnvelopeOverlap(CadBeamData a, CadBeamData b, BeamOverlapOptions opt = null)
        {
            if (a == null || b == null) return false;
            var options = opt ?? new BeamOverlapOptions();

            // 1. Angle Check
            double angleA = GetBeamAngle(a);
            double angleB = GetBeamAngle(b);
            double angleDiff = Math.Abs(angleA - angleB);
            if (angleDiff > Math.PI / 2.0) angleDiff = Math.PI - angleDiff;

            if (angleDiff * 180.0 / Math.PI > options.AngularToleranceDegrees)
            {
                return false;
            }

            // 2. Overlap Ratio Check
            double overlapRatio = ComputeOverlapRatio(a, b);
            if (overlapRatio < options.MinimumOverlapRatio)
            {
                return false;
            }

            // 3. Perpendicular Centerline Distance vs Physical Envelope
            double perpDist = ComputeCenterlineDistance(a, b);
            double halfWidthA = GetHalfWidth(a);
            double halfWidthB = GetHalfWidth(b);

            double maxEnvelopeDist = Math.Max(halfWidthA, halfWidthB) + 30.0; // EnvelopeToleranceMm = 30 mm

            return perpDist <= maxEnvelopeDist;
        }

        public static double GetHalfWidth(CadBeamData b)
        {
            if (b == null) return 0.0;
            if (b.Width > 0) return b.Width / 2.0;
            if (b.MeasuredWidth > 0) return b.MeasuredWidth / 2.0;
            return 0.0;
        }

        public static double ComputeCenterlineDistance(CadBeamData a, CadBeamData b)
        {
            double lenA = GetBeamLength(a);
            double lenB = GetBeamLength(b);
            if (lenA < 1e-3 || lenB < 1e-3) return double.MaxValue;

            double midAx = (a.StartX + a.EndX) / 2.0;
            double midAy = (a.StartY + a.EndY) / 2.0;
            double distAtoB = DistancePointToLine(midAx, midAy, b.StartX, b.StartY, b.EndX, b.EndY);

            double midBx = (b.StartX + b.EndX) / 2.0;
            double midBy = (b.StartY + b.EndY) / 2.0;
            double distBtoA = DistancePointToLine(midBx, midBy, a.StartX, a.StartY, a.EndX, a.EndY);

            return Math.Min(distAtoB, distBtoA);
        }

        public static double ComputeOverlapRatio(CadBeamData a, CadBeamData b)
        {
            double lenA = GetBeamLength(a);
            double lenB = GetBeamLength(b);
            if (lenA < 1e-3 || lenB < 1e-3) return 0.0;

            double ux = (a.EndX - a.StartX) / lenA;
            double uy = (a.EndY - a.StartY) / lenA;

            double tA_start = a.StartX * ux + a.StartY * uy;
            double tA_end = a.EndX * ux + a.EndY * uy;
            double minA = Math.Min(tA_start, tA_end);
            double maxA = Math.Max(tA_start, tA_end);

            double tB_start = b.StartX * ux + b.StartY * uy;
            double tB_end = b.EndX * ux + b.EndY * uy;
            double minB = Math.Min(tB_start, tB_end);
            double maxB = Math.Max(tB_start, tB_end);

            double overlapStart = Math.Max(minA, minB);
            double overlapEnd = Math.Min(maxA, maxB);
            double overlapLen = Math.Max(0.0, overlapEnd - overlapStart);

            double minLen = Math.Min(lenA, lenB);
            return minLen > 0 ? overlapLen / minLen : 0.0;
        }

        public static double ComputeOverlapLength(CadBeamData a, CadBeamData b)
        {
            double lenA = GetBeamLength(a);
            double lenB = GetBeamLength(b);
            if (lenA < 1e-3 || lenB < 1e-3) return 0.0;

            double ux = (a.EndX - a.StartX) / lenA;
            double uy = (a.EndY - a.StartY) / lenA;

            double minA = Math.Min(a.StartX * ux + a.StartY * uy, a.EndX * ux + a.EndY * uy);
            double maxA = Math.Max(a.StartX * ux + a.StartY * uy, a.EndX * ux + a.EndY * uy);

            double minB = Math.Min(b.StartX * ux + b.StartY * uy, b.EndX * ux + b.EndY * uy);
            double maxB = Math.Max(b.StartX * ux + b.StartY * uy, b.EndX * ux + b.EndY * uy);

            double overlapStart = Math.Max(minA, minB);
            double overlapEnd = Math.Min(maxA, maxB);
            return Math.Max(0.0, overlapEnd - overlapStart);
        }

        public static bool ComputeIsContained(CadBeamData a, CadBeamData b, double containmentToleranceMm = 50.0)
        {
            double lenA = GetBeamLength(a);
            double lenB = GetBeamLength(b);
            if (lenA < 1e-3 || lenB < 1e-3) return false;

            double ux = (a.EndX - a.StartX) / lenA;
            double uy = (a.EndY - a.StartY) / lenA;

            double minA = Math.Min(a.StartX * ux + a.StartY * uy, a.EndX * ux + a.EndY * uy);
            double maxA = Math.Max(a.StartX * ux + a.StartY * uy, a.EndX * ux + a.EndY * uy);

            double minB = Math.Min(b.StartX * ux + b.StartY * uy, b.EndX * ux + b.EndY * uy);
            double maxB = Math.Max(b.StartX * ux + b.StartY * uy, b.EndX * ux + b.EndY * uy);

            return (minA >= minB - containmentToleranceMm && maxA <= maxB + containmentToleranceMm)
                || (minB >= minA - containmentToleranceMm && maxB <= maxA + containmentToleranceMm);
        }

        private static double GetBeamLength(CadBeamData b)
        {
            double dx = b.EndX - b.StartX;
            double dy = b.EndY - b.StartY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double GetBeamAngle(CadBeamData b)
        {
            double len = GetBeamLength(b);
            if (len < 1e-9) return 0;
            double a = Math.Atan2(b.EndY - b.StartY, b.EndX - b.StartX);
            while (a < 0) a += Math.PI;
            while (a >= Math.PI) a -= Math.PI;
            return a;
        }

        private static double DistancePointToLine(double px, double py, double lx1, double ly1, double lx2, double ly2)
        {
            double dx = lx2 - lx1;
            double dy = ly2 - ly1;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-9) return Math.Sqrt((px - lx1) * (px - lx1) + (py - ly1) * (py - ly1));

            double cross = Math.Abs((lx2 - lx1) * (ly1 - py) - (lx1 - px) * (ly2 - ly1));
            return cross / Math.Sqrt(lenSq);
        }
    }
}
