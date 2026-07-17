using Antigravity.CadVoidPlacer.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Antigravity.CadVoidPlacer.Services
{
    /// <summary>
    /// Communicates with a live AutoCAD instance via COM automation.
    /// Supports DWG and DXF — no file export required.
    /// </summary>
    public class CadInteropService
    {
        private const double TOLERANCE = 5.0; // mm snap tolerance for line-chaining

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Prompts the user to select lines / polylines in AutoCAD, then
        /// chains them into closed polygon loops and returns CadOpening objects.
        /// </summary>
        public static (List<CadOpening> Openings, string FilePath)? SelectEntities()
        {
            try
            {
                dynamic acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                dynamic acadDoc = acadApp.ActiveDocument;
                string  docPath = acadDoc.FullName;
                dynamic utility = acadDoc.Utility;

                // Clean up any leftover selection set
                try { acadDoc.SelectionSets.Item("CVP_SEL").Delete(); } catch { }
                dynamic selSet = acadDoc.SelectionSets.Add("CVP_SEL");

                acadApp.Visible = true;
                utility.Prompt("\nSelect lines / polylines for void placement, then press Enter: ");
                selSet.SelectOnScreen();

                var entitiesResult = ((List<List<double[]>> Polygons, List<(double[] S, double[] E)> Lines))ProcessEntities(selSet, utility);
                var polygons = entitiesResult.Polygons;
                var rawLines = entitiesResult.Lines;

                // Chain individual line segments into closed loops
                var lineLoops = ChainLinesToLoops(rawLines);
                polygons.AddRange(lineLoops);

                try { selSet.Delete(); } catch { }

                var openings = new List<CadOpening>();
                foreach (var poly in polygons)
                    openings.Add(new CadOpening(poly));

                if (openings.Count == 0)
                {
                    utility.Prompt("\n[CVP] Không tìm thấy đa giác khép kín nào trong vùng chọn.");
                }
                else
                {
                    utility.Prompt($"\n[CVP] Đã nhận diện được {openings.Count} đa giác.");
                }

                return (openings, docPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Cannot connect to AutoCAD. Make sure AutoCAD is running with a document open.\n\n" + ex.Message);
            }
        }

        /// <summary>
        /// Retrieves the list of layer names from the active AutoCAD document.
        /// </summary>
        public static List<string> GetLayers()
        {
            var layers = new List<string>();
            try
            {
                dynamic acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                dynamic acadDoc = acadApp.ActiveDocument;
                foreach (dynamic layer in acadDoc.Layers)
                {
                    layers.Add((string)layer.Name);
                }
                layers.Sort();
            }
            catch { }
            return layers;
        }

        /// <summary>
        /// Scans the entire AutoCAD drawing for lines / polylines on a specific layer.
        /// </summary>
        public static List<CadOpening> ScanLayer(string layerName)
        {
            var results = new List<CadOpening>();
            try
            {
                dynamic acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                dynamic acadDoc = acadApp.ActiveDocument;

                // Clean up any leftover selection set
                try { acadDoc.SelectionSets.Item("CVP_SCAN").Delete(); } catch { }
                dynamic selSet = acadDoc.SelectionSets.Add("CVP_SCAN");

                // Filter for entities on the specified layer
                // In COM, SelectAll takes filter types and values as arrays.
                // 8 is the group code for layer name.
                short[] filterType = new short[] { 8 };
                object[] filterData = new object[] { layerName };

                selSet.Select(5, Type.Missing, Type.Missing, filterType, filterData); // 5 = acSelectionSetAll

                var scanResult = ((List<List<double[]>> Polygons, List<(double[] S, double[] E)> Lines))ProcessEntities(selSet, acadDoc.Utility);
                var polygons = scanResult.Polygons;
                var rawLines = scanResult.Lines;

                var lineLoops = ChainLinesToLoops(rawLines);
                polygons.AddRange(lineLoops);

                foreach (var poly in polygons)
                    results.Add(new CadOpening(poly, layerName));

                try { selSet.Delete(); } catch { }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error scanning AutoCAD layer: " + ex.Message);
            }
            return results;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Entity Processing Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static (List<List<double[]>> Polygons, List<(double[] S, double[] E)> Lines) 
            ProcessEntities(dynamic selSet, dynamic utility)
        {
            var polygons = new List<List<double[]>>();
            var rawLines = new List<(double[] S, double[] E)>();

            utility.Prompt($"\n[CVP] Đang xử lý {selSet.Count} đối tượng...");

            foreach (dynamic ent in selSet)
            {
                string etype = "";
                try { etype = (string)ent.EntityName; } catch { continue; }

                // LWPolyline (Standard Polyline)
                if (etype == "AcDbLWPolyline")
                {
                    try
                    {
                        double[] coords = (double[])ent.Coordinates;
                        var pts = new List<double[]>();
                        for (int i = 0; i + 1 < coords.Length; i += 2)
                            pts.Add(new[] { coords[i], coords[i + 1] });
                        
                        if (pts.Count >= 3) polygons.Add(pts);
                    }
                    catch { }
                }
                // 2D/3D Polyline (Old style)
                else if (etype == "AcDb2dPolyline" || etype == "AcDbPolyline" || etype.Contains("Polyline"))
                {
                    try
                    {
                        var pts = new List<double[]>();
                        // Try standard Coordinates property first
                        try
                        {
                            double[] coords = (double[])ent.Coordinates;
                            int stride = (coords.Length % 3 == 0) ? 3 : 2;
                            for (int i = 0; i + (stride - 1) < coords.Length; i += stride)
                                pts.Add(new[] { coords[i], coords[i + 1] });
                        }
                        catch
                        {
                            // Fallback to iterating vertices if available
                            foreach (dynamic v in ent.Vertices)
                            {
                                double[] c = (double[])v.Coordinates;
                                pts.Add(new[] { c[0], c[1] });
                            }
                        }
                        
                        if (pts.Count >= 3) polygons.Add(pts);
                    }
                    catch { }
                }
                else if (etype == "AcDbLine")
                {
                    try
                    {
                        double[] sp = (double[])ent.StartPoint;
                        double[] ep = (double[])ent.EndPoint;
                        rawLines.Add((new[] { sp[0], sp[1] }, new[] { ep[0], ep[1] }));
                    }
                    catch { }
                }
                // --- NEW: Block Reference Handling ---
                else if (etype == "AcDbBlockReference")
                {
                    try
                    {
                        // Explode the block in memory
                        dynamic explodedEntities = ent.Explode();
                        if (explodedEntities != null)
                        {
                            foreach (dynamic subEnt in explodedEntities)
                            {
                                string subType = (string)subEnt.EntityName;
                                if (subType.Contains("Polyline"))
                                {
                                    double[] coords = (double[])subEnt.Coordinates;
                                    var pts = new List<double[]>();
                                    int stride = (coords.Length % 3 == 0) ? 3 : 2;
                                    for (int i = 0; i + (stride - 1) < coords.Length; i += stride)
                                        pts.Add(new[] { coords[i], coords[i + 1] });
                                    if (pts.Count >= 3) polygons.Add(pts);
                                }
                                else if (subType == "AcDbLine")
                                {
                                    double[] sp = (double[])subEnt.StartPoint;
                                    double[] ep = (double[])subEnt.EndPoint;
                                    rawLines.Add((new[] { sp[0], sp[1] }, new[] { ep[0], ep[1] }));
                                }
                                try { subEnt.Delete(); } catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        utility.Prompt($"\n[CVP] Lỗi khi xử lý Block: {ex.Message}");
                    }
                }
            }

            return (polygons, rawLines);
        }

        /// <summary>
        /// Prompts the user to pick a single reference point in AutoCAD.
        /// Returns [X, Y, Z] in CAD units (mm for metric drawings).
        /// </summary>
        public static double[] PickReferencePoint()
        {
            try
            {
                dynamic acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                dynamic acadDoc = acadApp.ActiveDocument;
                dynamic utility = acadDoc.Utility;
                acadApp.Visible = true;
                var pt = utility.GetPoint(Type.Missing, "\nPick grid-intersection reference point: ");
                return pt as double[];
            }
            catch
            {
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Line-chaining algorithm
        // ─────────────────────────────────────────────────────────────────────

        private static List<List<double[]>> ChainLinesToLoops(
            List<(double[] S, double[] E)> lines)
        {
            var results   = new List<List<double[]>>();
            var remaining = new List<(double[] S, double[] E)>(lines);

            while (remaining.Count >= 3)
            {
                var loop = new List<double[]>();
                var seed = remaining[0];
                remaining.RemoveAt(0);
                loop.Add(seed.S);
                loop.Add(seed.E);

                bool extended = true;
                while (extended && remaining.Count > 0)
                {
                    extended = false;
                    double[] tip = loop[loop.Count - 1];

                    for (int i = 0; i < remaining.Count; i++)
                    {
                        var seg = remaining[i];
                        if (Dist(tip, seg.S) < TOLERANCE)
                        {
                            loop.Add(seg.E);
                            remaining.RemoveAt(i);
                            extended = true;
                            break;
                        }
                        if (Dist(tip, seg.E) < TOLERANCE)
                        {
                            loop.Add(seg.S);
                            remaining.RemoveAt(i);
                            extended = true;
                            break;
                        }
                    }

                    // Closed loop detected
                    if (loop.Count >= 4 && Dist(loop[loop.Count - 1], loop[0]) < TOLERANCE)
                    {
                        loop.RemoveAt(loop.Count - 1); // remove duplicate closing point
                        results.Add(loop);
                        break;
                    }
                }
            }

            return results;
        }

        private static double Dist(double[] a, double[] b) =>
            Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2));
    }
}
