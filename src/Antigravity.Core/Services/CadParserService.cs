using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.Core.Geometry;
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
            var candidateLoops = new List<IList<XYZ>>();

            ProcessGeometry(geomElem, doc, layerName, Transform.Identity, candidateLoops, looseLines);

            // Polygonize loose CAD lines by elevation. A shared undirected edge is
            // represented by two directed half-edges and can therefore bound two
            // adjacent foundation faces without being consumed globally.
            BuildLoops(looseLines, candidateLoops);

            foreach (var loop in candidateLoops)
            {
                ProcessPoints(loop, Transform.Identity, results);
            }

            return results;
        }

        private void BuildLoops(IList<Line> lines, ICollection<IList<XYZ>> candidateLoops)
        {
            const double elevationTolerance = 0.01;
            var extractor = new PlanarFaceExtractor(tolerance: 0.001, areaTolerance: 1e-8, maxInputSegments: 5000);

            var elevationGroups = lines.GroupBy(line =>
                (long)Math.Round(
                    ((line.GetEndPoint(0).Z + line.GetEndPoint(1).Z) / 2.0) / elevationTolerance,
                    MidpointRounding.AwayFromZero));

            foreach (var group in elevationGroups)
            {
                var sourceLines = group.ToList();
                if (sourceLines.Any(line => Math.Abs(line.GetEndPoint(0).Z - line.GetEndPoint(1).Z) > elevationTolerance))
                {
                    continue;
                }

                var elevation = sourceLines
                    .SelectMany(line => new[] { line.GetEndPoint(0).Z, line.GetEndPoint(1).Z })
                    .Average();
                var segments = sourceLines.Select(line => new Segment2(
                    new Point2(line.GetEndPoint(0).X, line.GetEndPoint(0).Y),
                    new Point2(line.GetEndPoint(1).X, line.GetEndPoint(1).Y)));

                var extraction = extractor.Extract(segments);
                foreach (var face in extraction.Faces)
                {
                    var points = face.Vertices.Select(point => new XYZ(point.X, point.Y, elevation)).ToList();
                    points.Add(points[0]);
                    candidateLoops.Add(points);
                }
            }
        }

        private void ProcessGeometry(GeometryElement geomElem, Document doc, string layerName, Transform currentTransform, List<IList<XYZ>> loops, List<Line> looseLines)
        {
            foreach (var geom in geomElem)
            {
                if (geom is GeometryInstance geomInst)
                {
                    var instanceGeom = geomInst.GetSymbolGeometry();
                    var newTransform = currentTransform.Multiply(geomInst.Transform);
                    ProcessGeometry(instanceGeom, doc, layerName, newTransform, loops, looseLines);
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

                                if (transformedPts.Count > 2 && transformedPts.First().DistanceTo(transformedPts.Last()) < 0.01)
                                {
                                    // It's a closed loop, add it directly. Do not explode into looseLines to preserve original outline.
                                    loops.Add(transformedPts);
                                }
                                else
                                {
                                    for (int i = 0; i < transformedPts.Count - 1; i++)
                                    {
                                        if (transformedPts[i].DistanceTo(transformedPts[i + 1]) > 0.01)
                                        {
                                            looseLines.Add(Line.CreateBound(transformedPts[i], transformedPts[i + 1]));
                                        }
                                    }
                                }
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

        private bool ProcessPoints(IList<XYZ> pts, Transform currentTransform, List<FoundationData> results)
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
                    return false;
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

                if (maxZ - minZ > 0.01)
                {
                    return false;
                }

                if (Math.Abs(longestEdge.Z) > 0.01)
                {
                    return false;
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
                    if (results.Any(r => r.Center.DistanceTo(center) < 0.1)) return false;

                    results.Add(new FoundationData { Center = center, RotationAngle = angle, Length = length, Width = width });
                    return true;
                }
            }
            return false;
        }
    }
}
