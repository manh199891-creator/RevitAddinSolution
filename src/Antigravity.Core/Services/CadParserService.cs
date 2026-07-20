using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.Core.Models;

namespace Antigravity.Core.Services
{
    public class CadParserService
    {
        public List<FoundationData> ExtractFoundationData(ImportInstance cadLink, string layerName)
        {
            var results = new List<FoundationData>();
            var doc = cadLink.Document;
            var geomElem = cadLink.get_Geometry(new Options());

            if (geomElem == null) return results;

            var looseLines = new List<Line>();
            ProcessGeometry(geomElem, doc, layerName, Transform.Identity, results, looseLines);

            // Process loose lines into polygons
            var loops = BuildLoops(looseLines);
            foreach (var loop in loops)
            {
                var pts = loop.Select(l => l.GetEndPoint(0)).ToList();
                pts.Add(loop.Last().GetEndPoint(1)); // Add the closing point
                ProcessPoints(pts, Transform.Identity, results);
            }

            return results;
        }

        private List<List<Line>> BuildLoops(List<Line> lines)
        {
            var loops = new List<List<Line>>();
            var remaining = new List<Line>(lines);

            while (remaining.Count > 0)
            {
                var currentLoop = new List<Line>();
                var currentLine = remaining[0];
                currentLoop.Add(currentLine);
                remaining.RemoveAt(0);

                XYZ currentEnd = currentLine.GetEndPoint(1);

                bool added = true;
                while (added)
                {
                    added = false;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        var nextLine = remaining[i];
                        var p0 = nextLine.GetEndPoint(0);
                        var p1 = nextLine.GetEndPoint(1);

                        if (p0.DistanceTo(currentEnd) < 0.01)
                        {
                            currentLoop.Add(nextLine);
                            currentEnd = p1;
                            remaining.RemoveAt(i);
                            added = true;
                            break;
                        }
                        else if (p1.DistanceTo(currentEnd) < 0.01)
                        {
                            // Reverse the line
                            var reversed = Line.CreateBound(p1, p0);
                            currentLoop.Add(reversed);
                            currentEnd = p0;
                            remaining.RemoveAt(i);
                            added = true;
                            break;
                        }
                    }
                }

                if (currentLoop.Count >= 4 && currentLoop[0].GetEndPoint(0).DistanceTo(currentEnd) < 0.01)
                {
                    loops.Add(currentLoop);
                }
            }

            return loops;
        }

        private void ProcessGeometry(GeometryElement geomElem, Document doc, string layerName, Transform currentTransform, List<FoundationData> results, List<Line> looseLines)
        {
            foreach (var geom in geomElem)
            {
                if (geom is GeometryInstance geomInst)
                {
                    var instanceGeom = geomInst.GetSymbolGeometry();
                    var newTransform = currentTransform.Multiply(geomInst.Transform);
                    ProcessGeometry(instanceGeom, doc, layerName, newTransform, results, looseLines);
                }
                else
                {
                    var styleId = geom.GraphicsStyleId;
                    if (styleId != ElementId.InvalidElementId)
                    {
                        var style = doc.GetElement(styleId) as GraphicsStyle;
                        if (style != null && style.GraphicsStyleCategory.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (geom is PolyLine polyLine)
                            {
                                var pts = polyLine.GetCoordinates();
                                var transformedPts = pts.Select(p => currentTransform.OfPoint(p)).ToList();
                                ProcessPoints(transformedPts, Transform.Identity, results);
                            }
                            else if (geom is Line line)
                            {
                                var p0 = currentTransform.OfPoint(line.GetEndPoint(0));
                                var p1 = currentTransform.OfPoint(line.GetEndPoint(1));
                                if (p0.DistanceTo(p1) > 0.01)
                                {
                                    looseLines.Add(Line.CreateBound(p0, p1));
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ProcessPoints(IList<XYZ> pts, Transform currentTransform, List<FoundationData> results)
        {
            if (pts.Count >= 4 && pts[0].DistanceTo(pts[pts.Count - 1]) < 0.01)
            {
                int corners = 0;
                bool orthogonal = true;
                XYZ lastDir = XYZ.Zero;
                XYZ firstValidDir = XYZ.Zero;
                
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var p1 = currentTransform.OfPoint(pts[i]);
                    var p2 = currentTransform.OfPoint(pts[i + 1]);
                    if (p1.DistanceTo(p2) < 0.001) continue;
                    
                    var dir = (p2 - p1).Normalize();
                    if (firstValidDir.IsZeroLength())
                    {
                        firstValidDir = dir;
                    }

                    if (lastDir.IsZeroLength())
                    {
                        lastDir = dir;
                    }
                    else
                    {
                        double rawDot = lastDir.DotProduct(dir);
                        if (Math.Abs(rawDot) < 0.01)
                        {
                            corners++;
                            lastDir = dir;
                        }
                        else if (rawDot > 0.99)
                        {
                            // collinear and same direction
                        }
                        else
                        {
                            orthogonal = false;
                            break;
                        }
                    }
                }
                
                if (!lastDir.IsZeroLength() && !firstValidDir.IsZeroLength())
                {
                    if (Math.Abs(lastDir.DotProduct(firstValidDir)) < 0.01)
                    {
                        corners++;
                    }
                }

                if (!orthogonal || corners != 4)
                {
                    return;
                }

                double minZ = double.MaxValue;
                double maxZ = double.MinValue;
                double maxLen = 0;
                XYZ longestEdge = XYZ.BasisX;

                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var p1 = currentTransform.OfPoint(pts[i]);
                    if (p1.Z < minZ) minZ = p1.Z;
                    if (p1.Z > maxZ) maxZ = p1.Z;

                    var p2 = currentTransform.OfPoint(pts[i + 1]);
                    var len = p1.DistanceTo(p2);
                    if (len > maxLen) { maxLen = len; longestEdge = (p2 - p1).Normalize(); }
                }

                if (Math.Abs(longestEdge.Z) > 0.01)
                {
                    return;
                }

                var angle = longestEdge.AngleTo(XYZ.BasisX);
                if (longestEdge.Y < 0) angle = -angle;

                var crossDir = XYZ.BasisZ.CrossProduct(longestEdge).Normalize();

                double minProjU = double.MaxValue, maxProjU = double.MinValue;
                double minProjV = double.MaxValue, maxProjV = double.MinValue;

                foreach (var p in pts)
                {
                    var pt = currentTransform.OfPoint(p);
                    double u = pt.DotProduct(longestEdge);
                    double v = pt.DotProduct(crossDir);

                    if (u < minProjU) minProjU = u;
                    if (u > maxProjU) maxProjU = u;
                    if (v < minProjV) minProjV = v;
                    if (v > maxProjV) maxProjV = v;
                }

                double length = maxProjU - minProjU;
                double width = maxProjV - minProjV;

                double centerU = (minProjU + maxProjU) / 2;
                double centerV = (minProjV + maxProjV) / 2;
                double avgZ = currentTransform.OfPoint(pts[0]).Z;

                var center = XYZ.Zero + centerU * longestEdge + centerV * crossDir + new XYZ(0, 0, avgZ);

                if (length > 0.1 && width > 0.1)
                {
                    results.Add(new FoundationData { Center = center, RotationAngle = angle, Length = length, Width = width });
                }
            }
        }
    }
}
