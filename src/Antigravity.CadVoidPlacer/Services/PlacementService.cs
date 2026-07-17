using Autodesk.Revit.DB;
using Antigravity.CadVoidPlacer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.CadVoidPlacer.Services
{
    /// <summary>
    /// Places structural openings as DirectShape solids (Generic Model category).
    /// Supports arbitrary polygon prisms — no Revit family (.rfa) required.
    /// </summary>
    public class PlacementService
    {
        public static int PlaceVoids(
            Document doc,
            List<CadOpening> openings,
            GridMappingService mapper,
            double depthMm = 300.0,
            ElementId levelId = null)
        {
            if (doc == null || openings == null || mapper == null) return 0;

            // Resolve elevation offset from the selected level
            double levelElevFt = 0.0;
            if (levelId != null && levelId != ElementId.InvalidElementId)
            {
                var lvl = doc.GetElement(levelId) as Level;
                if (lvl != null) levelElevFt = lvl.Elevation;
            }

            int placed   = 0;
            double depFt = GridMappingService.MmToFeet(depthMm);

            using (var trans = new Transaction(doc, "Place CAD Void DirectShapes"))
            {
                trans.Start();

                foreach (var opening in openings)
                {
                    try
                    {
                        Solid solid = null;

                        if (opening.IsPolygon)
                        {
                            // ── Polygon prism (arbitrary shape from CAD) ──────────
                            XYZ[] bottomPts = mapper.CadVerticesToRevit(opening.Vertices, levelElevFt);
                            solid = CreatePrismSolid(bottomPts, depFt);

                            // ── Fallback to Bounding Box if Prism fails ─────────
                            if (solid == null)
                            {
                                double minX = bottomPts.Min(p => p.X);
                                double maxX = bottomPts.Max(p => p.X);
                                double minY = bottomPts.Min(p => p.Y);
                                double maxY = bottomPts.Max(p => p.Y);
                                
                                XYZ center = new XYZ((minX + maxX) / 2.0, (minY + maxY) / 2.0, levelElevFt);
                                double wFt = maxX - minX;
                                double lFt = maxY - minY;
                                
                                // To avoid zero-width/length issues with straight lines misidentified as loops
                                if (wFt < 0.1) wFt = 0.1;
                                if (lFt < 0.1) lFt = 0.1;

                                solid = CreateBoxSolid(center, wFt, lFt, depFt);
                            }
                        }
                        else
                        {
                            // ── Fallback: axis-aligned box ───────────────────────
                            XYZ center = mapper.CadToRevit(opening.CenterX, opening.CenterY, levelElevFt);
                            double wFt  = GridMappingService.MmToFeet(opening.Width);
                            double lFt  = GridMappingService.MmToFeet(opening.Length);
                            solid = CreateBoxSolid(center, wFt, lFt, depFt);
                        }

                        if (solid == null) continue;

                        var ds = DirectShape.CreateElement(
                            doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.SetShape(new GeometryObject[] { solid });

                        // Name includes vertex count + depth so it is distinguishable in schedules
                        int vtxCount = opening.IsPolygon ? opening.Vertices.Count : 4;
                        int d = (int)Math.Round(depthMm);
                        ds.Name = $"CAD Void {vtxCount}pts D{d}mm";

                        placed++;
                    }
                    catch
                    {
                        // Skip individual failures — continue with the rest
                    }
                }

                trans.Commit();
            }

            return placed;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Geometry builders
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a prism solid extruded from an arbitrary polygon base.
        /// Vertices are automatically normalised to CCW winding order.
        /// </summary>
        private static Solid CreatePrismSolid(XYZ[] bottom, double hFt)
        {
            // 1. Cleanup points: remove duplicates and collinear points
            var cleanPts = new List<XYZ>();
            double tol = 0.001; // ~0.3mm tolerance

            for (int i = 0; i < bottom.Length; i++)
            {
                XYZ pt = bottom[i];
                if (cleanPts.Count == 0 || cleanPts.Last().DistanceTo(pt) > tol)
                {
                    cleanPts.Add(pt);
                }
            }

            // Remove last point if it's the same as first
            if (cleanPts.Count > 1 && cleanPts.Last().DistanceTo(cleanPts.First()) < tol)
                cleanPts.RemoveAt(cleanPts.Count - 1);

            // Remove collinear points
            var finalPts = new List<XYZ>();
            for (int i = 0; i < cleanPts.Count; i++)
            {
                XYZ prev = cleanPts[(i - 1 + cleanPts.Count) % cleanPts.Count];
                XYZ curr = cleanPts[i];
                XYZ next = cleanPts[(i + 1) % cleanPts.Count];

                XYZ dir1 = (curr - prev).Normalize();
                XYZ dir2 = (next - curr).Normalize();

                // If cross product is essentially zero, points are collinear
                if (dir1.CrossProduct(dir2).GetLength() > 1e-5)
                {
                    finalPts.Add(curr);
                }
            }

            if (finalPts.Count < 3) return null;
            bottom = finalPts.ToArray();
            int n = bottom.Length;

            // Ensure CCW winding (viewed from +Z) — compute 2-D signed area
            double area = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += bottom[i].X * bottom[j].Y - bottom[j].X * bottom[i].Y;
            }
            if (area < 0) Array.Reverse(bottom); // CW → flip to CCW

            XYZ[] top = bottom.Select(p => new XYZ(p.X, p.Y, p.Z + hFt)).ToArray();

            var builder = new TessellatedShapeBuilder();
            builder.OpenConnectedFaceSet(true);

            // Bottom face — outward normal = -Z → vertices in CW order (= reverse CCW)
            var botFace = bottom.Reverse().ToArray();
            builder.AddFace(new TessellatedFace(botFace, ElementId.InvalidElementId));

            // Top face — outward normal = +Z → CCW order
            builder.AddFace(new TessellatedFace(top, ElementId.InvalidElementId));

            // Side faces
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                builder.AddFace(new TessellatedFace(
                    new[] { bottom[i], bottom[j], top[j], top[i] },
                    ElementId.InvalidElementId));
            }

            builder.CloseConnectedFaceSet();
            builder.Target   = TessellatedShapeBuilderTarget.Solid;
            builder.Fallback = TessellatedShapeBuilderFallback.Abort;
            builder.Build();

            var result = builder.GetBuildResult();
            if (result.Outcome != TessellatedShapeBuilderOutcome.Solid) return null;

            var solids = result.GetGeometricalObjects();
            return (solids != null && solids.Count > 0) ? solids[0] as Solid : null;
        }

        /// <summary>Axis-aligned rectangular box (legacy / fallback).</summary>
        private static Solid CreateBoxSolid(XYZ center, double wFt, double lFt, double hFt)
        {
            double hw = wFt / 2.0, hl = lFt / 2.0;
            XYZ p0 = new XYZ(center.X - hw, center.Y - hl, center.Z);
            XYZ p1 = new XYZ(center.X + hw, center.Y - hl, center.Z);
            XYZ p2 = new XYZ(center.X + hw, center.Y + hl, center.Z);
            XYZ p3 = new XYZ(center.X - hw, center.Y + hl, center.Z);
            XYZ p4 = new XYZ(center.X - hw, center.Y - hl, center.Z + hFt);
            XYZ p5 = new XYZ(center.X + hw, center.Y - hl, center.Z + hFt);
            XYZ p6 = new XYZ(center.X + hw, center.Y + hl, center.Z + hFt);
            XYZ p7 = new XYZ(center.X - hw, center.Y + hl, center.Z + hFt);

            var builder = new TessellatedShapeBuilder();
            builder.OpenConnectedFaceSet(true);
            builder.AddFace(new TessellatedFace(new[] { p0, p3, p2, p1 }, ElementId.InvalidElementId));
            builder.AddFace(new TessellatedFace(new[] { p4, p5, p6, p7 }, ElementId.InvalidElementId));
            builder.AddFace(new TessellatedFace(new[] { p0, p1, p5, p4 }, ElementId.InvalidElementId));
            builder.AddFace(new TessellatedFace(new[] { p2, p3, p7, p6 }, ElementId.InvalidElementId));
            builder.AddFace(new TessellatedFace(new[] { p0, p4, p7, p3 }, ElementId.InvalidElementId));
            builder.AddFace(new TessellatedFace(new[] { p1, p2, p6, p5 }, ElementId.InvalidElementId));
            builder.CloseConnectedFaceSet();
            builder.Target   = TessellatedShapeBuilderTarget.Solid;
            builder.Fallback = TessellatedShapeBuilderFallback.Abort;
            builder.Build();

            var result = builder.GetBuildResult();
            if (result.Outcome != TessellatedShapeBuilderOutcome.Solid) return null;
            var solids = result.GetGeometricalObjects();
            return (solids != null && solids.Count > 0) ? solids[0] as Solid : null;
        }
    }
}
