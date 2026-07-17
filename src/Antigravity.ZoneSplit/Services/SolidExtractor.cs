using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.ZoneSplit.Services
{
    public static class SolidExtractor
    {
        private static readonly Options _opts = new Options
        {
            ComputeReferences = false,
            IncludeNonVisibleObjects = false,
            DetailLevel = ViewDetailLevel.Fine,
        };

        public static Solid GetLargestSolid(Element elem)
        {
            if (elem == null) return null;
            var geom = elem.get_Geometry(_opts);
            if (geom == null) return null;

            return CollectSolids(geom)
                .Where(s => s.Volume > 1e-9)
                .OrderByDescending(s => s.Volume)
                .FirstOrDefault();
        }

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
            {
                var union = SafeBooleanUnion(result, solids[i]);
                if (union != null) result = union;
            }

            return result;
        }

        private static IEnumerable<Solid> CollectSolids(GeometryElement geomElem)
        {
            if (geomElem == null) yield break;
            foreach (var obj in geomElem)
            {
                if (obj is Solid s && s.Volume > 1e-9)
                {
                    yield return s;
                }
                else if (obj is GeometryInstance inst)
                {
                    foreach (var subS in CollectSolids(inst.GetInstanceGeometry()))
                        yield return subS;
                }
            }
        }

        private static Solid SafeBooleanUnion(Solid a, Solid b)
        {
            try { return BooleanOperationsUtils.ExecuteBooleanOperation(a, b, BooleanOperationsType.Union); }
            catch { return null; }
        }

        public static double IntersectionVolume(Solid a, Solid b)
        {
            var inter = SafeIntersectionWithFallback(a, b);
            return inter?.Volume ?? 0;
        }

        public static Solid IntersectionSolid(Solid a, Solid b)
        {
            var inter = SafeIntersectionWithFallback(a, b);
            return (inter != null && inter.Volume > 1e-9) ? inter : null;
        }

        private static Solid SafeIntersectionWithFallback(Solid a, Solid b)
        {
            if (a == null || b == null) return null;

            // Attempt 1: Direct intersection
            try
            {
                var inter = BooleanOperationsUtils.ExecuteBooleanOperation(a, b, BooleanOperationsType.Intersect);
                if (inter != null && inter.Volume > 1e-9) return inter;
            }
            catch { /* Ignored, will try fallback */ }

            Solid bestFallback = null;

            // Attempt 2: Micro-translation in XY plane
            try
            {
                var transformXY = Transform.CreateTranslation(new XYZ(1e-5, 1e-5, 0));
                var bXY = SolidUtils.CreateTransformed(b, transformXY);
                var interXY = BooleanOperationsUtils.ExecuteBooleanOperation(a, bXY, BooleanOperationsType.Intersect);
                bestFallback = SelectLargerIntersection(bestFallback, interXY);
            }
            catch { /* Ignored */ }

            // Attempt 3: Micro-translation in Z axis
            try
            {
                var transformZ = Transform.CreateTranslation(new XYZ(0, 0, 1e-5));
                var bZ = SolidUtils.CreateTransformed(b, transformZ);
                var interZ = BooleanOperationsUtils.ExecuteBooleanOperation(a, bZ, BooleanOperationsType.Intersect);
                bestFallback = SelectLargerIntersection(bestFallback, interZ);
            }
            catch { /* Failed */ }

            // Attempt 4: Larger translation 1mm (0.00328 ft)
            try
            {
                var transformL = Transform.CreateTranslation(new XYZ(0.003, 0.003, 0.003));
                var bL = SolidUtils.CreateTransformed(b, transformL);
                var interL = BooleanOperationsUtils.ExecuteBooleanOperation(a, bL, BooleanOperationsType.Intersect);
                bestFallback = SelectLargerIntersection(bestFallback, interL);
            }
            catch { /* Failed */ }

            // Attempt 5: Even larger translation 5mm (0.016 ft)
            try
            {
                var transformXL = Transform.CreateTranslation(new XYZ(-0.016, -0.016, -0.016));
                var bXL = SolidUtils.CreateTransformed(b, transformXL);
                var interXL = BooleanOperationsUtils.ExecuteBooleanOperation(a, bXL, BooleanOperationsType.Intersect);
                bestFallback = SelectLargerIntersection(bestFallback, interXL);
            }
            catch { /* Failed all attempts */ }

            return bestFallback;
        }

        private static Solid SelectLargerIntersection(Solid current, Solid candidate)
        {
            if (candidate == null || candidate.Volume <= 1e-9) return current;
            if (current == null || candidate.Volume > current.Volume) return candidate;
            return current;
        }
    }
}
