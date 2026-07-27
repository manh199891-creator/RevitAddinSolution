using Antigravity.DrawBeams.Models;
using System;

namespace Antigravity.DrawBeams.Services
{
    public static class RevitBeamGuardHelper
    {
        public static bool IsDuplicateRevitBeam(
            double candStartX, double candStartY, double candEndX, double candEndY, double candWidth, double candHeight,
            double existStartX, double existStartY, double existEndX, double existEndY, double existWidth, double existHeight,
            BeamOverlapOptions options = null)
        {
            var opt = options ?? new BeamOverlapOptions();

            // 1. Dimension Match Check
            bool sameWidth = Math.Abs(candWidth - existWidth) <= 5.0;
            bool sameHeight = candHeight <= 0 || existHeight <= 0 || Math.Abs(candHeight - existHeight) <= 5.0;
            if (!sameWidth || !sameHeight) return false;

            // 2. Geometry Length Check
            double lenC = Math.Sqrt((candEndX - candStartX) * (candEndX - candStartX) + (candEndY - candStartY) * (candEndY - candStartY));
            double lenE = Math.Sqrt((existEndX - existStartX) * (existEndX - existStartX) + (existEndY - existStartY) * (existEndY - existStartY));
            if (lenC < 1e-3 || lenE < 1e-3) return false;

            // 3. Angle Check
            double angleC = Math.Atan2(candEndY - candStartY, candEndX - candStartX);
            while (angleC < 0) angleC += Math.PI;
            while (angleC >= Math.PI) angleC -= Math.PI;

            double angleE = Math.Atan2(existEndY - existStartY, existEndX - existStartX);
            while (angleE < 0) angleE += Math.PI;
            while (angleE >= Math.PI) angleE -= Math.PI;

            double angleDiff = Math.Abs(angleC - angleE);
            if (angleDiff > Math.PI / 2.0) angleDiff = Math.PI - angleDiff;
            if (angleDiff * 180.0 / Math.PI > opt.AngularToleranceDegrees) return false;

            // 4. Perpendicular Centerline Distance
            double midCx = (candStartX + candEndX) / 2.0;
            double midCy = (candStartY + candEndY) / 2.0;

            double cross = Math.Abs((existEndX - existStartX) * (existStartY - midCy) - (existStartX - midCx) * (existEndY - existStartY));
            double perpDist = cross / lenE;

            if (perpDist > opt.CenterlineDistanceToleranceMm) return false;

            // 5. 1D Overlap Ratio & Containment
            double ux = (candEndX - candStartX) / lenC;
            double uy = (candEndY - candStartY) / lenC;

            double tC1 = candStartX * ux + candStartY * uy;
            double tC2 = candEndX * ux + candEndY * uy;
            double minC = Math.Min(tC1, tC2);
            double maxC = Math.Max(tC1, tC2);

            double tE1 = existStartX * ux + existStartY * uy;
            double tE2 = existEndX * ux + existEndY * uy;
            double minE = Math.Min(tE1, tE2);
            double maxE = Math.Max(tE1, tE2);

            double overlapStart = Math.Max(minC, minE);
            double overlapEnd = Math.Min(maxC, maxE);
            double overlapLen = Math.Max(0.0, overlapEnd - overlapStart);

            double minLen = Math.Min(lenC, lenE);
            double overlapRatio = minLen > 0 ? overlapLen / minLen : 0;

            bool isContained = (minC >= minE - opt.ContainmentToleranceMm && maxC <= maxE + opt.ContainmentToleranceMm)
                            || (minE >= minC - opt.ContainmentToleranceMm && maxE <= maxC + opt.ContainmentToleranceMm);

            return overlapRatio >= opt.MinimumOverlapRatio || isContained;
        }
    }
}
