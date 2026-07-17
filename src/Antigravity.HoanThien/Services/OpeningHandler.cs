using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.HoanThien.Services
{
    public class OpeningHandler
    {
        public static List<(XYZ pt, double halfWidth)> GetOpenings(Wall wall, Document doc)
        {
            var openings = new List<(XYZ pt, double halfWidth)>();
            if (wall == null) return openings;

            var inserts = wall.FindInserts(true, false, false, false);
            foreach (var insertId in inserts)
            {
                var elem = doc.GetElement(insertId);
                if (elem is FamilyInstance fi && (fi.Category.Id.Value == (int)BuiltInCategory.OST_Doors || fi.Category.Id.Value == (int)BuiltInCategory.OST_Windows))
                {
                    var loc = fi.Location as LocationPoint;
                    if (loc != null)
                    {
                        double width = 0;
                        var widthParam = fi.Symbol.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM);
                        if (widthParam != null) width = widthParam.AsDouble();
                        else
                        {
                            var typeWidthParam = fi.Symbol.LookupParameter("Width");
                            if (typeWidthParam != null) width = typeWidthParam.AsDouble();
                        }
                        
                        if (width > 0)
                        {
                            openings.Add((loc.Point, width / 2.0));
                        }
                    }
                }
            }
            return openings;
        }

        public static List<Curve> SplitCurveAroundOpening(Curve wallCurve, List<(XYZ pt, double halfWidth)> openings)
        {
            // Implementation of 1D projection splitting.
            var results = new List<Curve>();
            if (openings == null || openings.Count == 0 || !(wallCurve is Line line))
            {
                results.Add(wallCurve);
                return results;
            }

            var dir = line.Direction;
            var origin = line.GetEndPoint(0);
            double totalLen = line.Length;
            
            // Map openings to 1D parameters along the line
            var intervals = new List<Tuple<double, double>>();
            foreach (var op in openings)
            {
                double t = dir.DotProduct(op.pt - origin);
                double tStart = Math.Max(0, t - op.halfWidth);
                double tEnd = Math.Min(totalLen, t + op.halfWidth);
                if (tEnd > tStart)
                {
                    intervals.Add(new Tuple<double, double>(tStart, tEnd));
                }
            }

            // Sort and merge intervals
            intervals = intervals.OrderBy(x => x.Item1).ToList();
            var merged = new List<Tuple<double, double>>();
            foreach (var iv in intervals)
            {
                if (merged.Count == 0) merged.Add(iv);
                else
                {
                    var last = merged.Last();
                    if (iv.Item1 <= last.Item2)
                    {
                        merged[merged.Count - 1] = new Tuple<double, double>(last.Item1, Math.Max(last.Item2, iv.Item2));
                    }
                    else
                    {
                        merged.Add(iv);
                    }
                }
            }

            // Create remaining curves
            double currentT = 0;
            foreach (var iv in merged)
            {
                if (iv.Item1 - currentT >= 0.05) // R1 Mitigation: Bỏ qua nếu curve < 0.05ft
                {
                    var p1 = origin + dir * currentT;
                    var p2 = origin + dir * iv.Item1;
                    results.Add(Line.CreateBound(p1, p2));
                }
                currentT = iv.Item2;
            }

            if (totalLen - currentT >= 0.05)
            {
                var p1 = origin + dir * currentT;
                var p2 = origin + dir * totalLen;
                results.Add(Line.CreateBound(p1, p2));
            }

            return results;
        }
    }
}
