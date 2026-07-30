using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.DrawColumns.Services
{
    public class ColumnCadData
    {
        public bool IsCircle { get; set; }
        public XYZ Centroid { get; set; }
        public double Dimension1 { get; set; } // B or Diameter
        public double Dimension2 { get; set; } // H
    }

    public interface ICadColumnParserService
    {
        List<ColumnCadData> ExtractColumns(ImportInstance importInstance);
    }

    public class CadColumnParserService : ICadColumnParserService
    {
        public List<ColumnCadData> ExtractColumns(ImportInstance importInstance)
        {
            var results = new List<ColumnCadData>();
            var options = new Options { ComputeReferences = true };
            var geomElement = importInstance.get_Geometry(options);
            if (geomElement == null) return results;

            var curves = new List<Curve>();
            ExtractCurvesRecursive(geomElement, importInstance.GetTransform(), curves);

            // Group into rectangles and circles
            var circles = curves.OfType<Arc>().Where(a => a.IsBound && Math.Abs(Math.Abs(a.GetEndParameter(1) - a.GetEndParameter(0)) - 2 * Math.PI) < 0.001).ToList();
            foreach (var circle in circles)
            {
                results.Add(new ColumnCadData
                {
                    IsCircle = true,
                    Centroid = circle.Center,
                    Dimension1 = circle.Radius * 2
                });
            }

            var lines = curves.OfType<Line>().ToList();
            // Simple grouping of lines into rectangles
            var unused = new HashSet<Line>(lines);
            while (unused.Count >= 4)
            {
                var start = unused.First();
                unused.Remove(start);
                
                // Find connected lines
                var loop = new List<Line> { start };
                XYZ currentEnd = start.GetEndPoint(1);
                
                for (int i = 0; i < 3; i++)
                {
                    var next = unused.FirstOrDefault(l => l.GetEndPoint(0).IsAlmostEqualTo(currentEnd) || l.GetEndPoint(1).IsAlmostEqualTo(currentEnd));
                    if (next != null)
                    {
                        loop.Add(next);
                        unused.Remove(next);
                        currentEnd = next.GetEndPoint(0).IsAlmostEqualTo(currentEnd) ? next.GetEndPoint(1) : next.GetEndPoint(0);
                    }
                }
                
                if (loop.Count == 4 && currentEnd.IsAlmostEqualTo(start.GetEndPoint(0)))
                {
                    // It's a closed 4-line loop. Assuming rectangle for Auto Column.
                    double len1 = loop[0].Length;
                    double len2 = loop[1].Length;
                    XYZ centroid = (loop[0].GetEndPoint(0) + loop[0].GetEndPoint(1) + loop[2].GetEndPoint(0) + loop[2].GetEndPoint(1)) / 4.0;
                    results.Add(new ColumnCadData
                    {
                        IsCircle = false,
                        Centroid = centroid,
                        Dimension1 = Math.Min(len1, len2),
                        Dimension2 = Math.Max(len1, len2)
                    });
                }
            }

            return results;
        }

        private void ExtractCurvesRecursive(GeometryElement geomElement, Transform currentTransform, List<Curve> curves)
        {
            foreach (var obj in geomElement)
            {
                if (obj is GeometryInstance instance)
                {
                    ExtractCurvesRecursive(instance.GetInstanceGeometry(), currentTransform.Multiply(instance.Transform), curves);
                }
                else if (obj is Curve curve)
                {
                    curves.Add(curve.CreateTransformed(currentTransform));
                }
            }
        }
    }
}


