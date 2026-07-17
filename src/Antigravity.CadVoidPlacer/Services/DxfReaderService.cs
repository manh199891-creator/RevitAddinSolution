using netDxf;
using netDxf.Entities;
using System.Collections.Generic;
using Antigravity.CadVoidPlacer.Models;
using System.Linq;
using System;

namespace Antigravity.CadVoidPlacer.Services
{
    public class DxfReaderService
    {
        public static List<string> GetLayers(string filePath)
        {
            try
            {
                var dxf = DxfDocument.Load(filePath);
                if (dxf == null) return new List<string>();
                return dxf.Layers.Select(l => l.Name).OrderBy(n => n).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static List<CadOpening> ExtractOpenings(string filePath, string layerName)
        {
            var openings = new List<CadOpening>();
            var dxf = DxfDocument.Load(filePath);
            if (dxf == null) return openings;

            // 1. Process Polylines (Already connected)
            foreach (var poly in dxf.Entities.Polylines2D.Where(e => e.Layer.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase)))
            {
                var opening = CreateFromPolyline2D(poly);
                if (opening != null) openings.Add(opening);
            }

            // 2. Process Separate Lines (Connect points to form boxes)
            var lineOpenings = ExtractFromLines(dxf.Entities.Lines.Where(e => e.Layer.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase)).ToList());
            openings.AddRange(lineOpenings);

            return openings;
        }

        private static List<CadOpening> ExtractFromLines(List<Line> lines)
        {
            var results = new List<CadOpening>();
            if (lines.Count < 4) return results;

            // Simple algorithm: Group lines by endpoints to find closed loops
            // For structural openings, they are usually 4 lines forming a rectangle.
            
            var usedLines = new HashSet<Line>();
            
            foreach (var line in lines)
            {
                if (usedLines.Contains(line)) continue;

                var currentLoop = new List<Line> { line };
                usedLines.Add(line);
                
                bool foundNext = true;
                while (foundNext && currentLoop.Count < 10) // Limit search
                {
                    foundNext = false;
                    var lastPt = currentLoop.Last().EndPoint;
                    
                    var nextLine = lines.FirstOrDefault(l => !usedLines.Contains(l) && 
                        (IsNear(l.StartPoint, lastPt) || IsNear(l.EndPoint, lastPt)));
                    
                    if (nextLine != null)
                    {
                        // Ensure direction
                        if (IsNear(nextLine.EndPoint, lastPt))
                        {
                            // Swap start/end for consistency if needed (simplified here)
                        }
                        currentLoop.Add(nextLine);
                        usedLines.Add(nextLine);
                        foundNext = true;

                        // Check if closed
                        if (IsNear(currentLoop.Last().EndPoint, currentLoop.First().StartPoint) && currentLoop.Count >= 3)
                        {
                            var opening = CreateFromLineLoop(currentLoop);
                            if (opening != null) results.Add(opening);
                            break;
                        }
                    }
                }
            }
            return results;
        }

        private static bool IsNear(Vector3 p1, Vector3 p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2)) < 1.0; // 1mm tolerance
        }

        private static CadOpening CreateFromLineLoop(List<Line> loop)
        {
            var pts = new List<Vector2>();
            foreach (var l in loop)
            {
                pts.Add(new Vector2(l.StartPoint.X, l.StartPoint.Y));
                pts.Add(new Vector2(l.EndPoint.X, l.EndPoint.Y));
            }
            return CalculateBoundingBox(pts);
        }

        private static CadOpening CreateFromPolyline2D(Polyline2D poly)
        {
            if (poly.Vertexes.Count < 3) return null;
            var positions = poly.Vertexes.Select(v => v.Position).ToList();
            return CalculateBoundingBox(positions);
        }

        private static CadOpening CalculateBoundingBox(List<Vector2> positions)
        {
            double minX = positions.Min(v => v.X);
            double maxX = positions.Max(v => v.X);
            double minY = positions.Min(v => v.Y);
            double maxY = positions.Max(v => v.Y);

            double width = maxX - minX;
            double length = maxY - minY;

            if (width < 0.1 || length < 0.1) return null;

            return new CadOpening((minX + maxX) / 2.0, (minY + maxY) / 2.0, width, length);
        }
    }
}
