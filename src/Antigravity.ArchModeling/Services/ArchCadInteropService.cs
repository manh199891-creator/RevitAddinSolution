using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Antigravity.ArchModeling.Models;
using Autodesk.Revit.DB;

namespace Antigravity.ArchModeling.Services
{
    public class ArchCadInteropService
    {
        private dynamic _acadApp;
        private dynamic _acadDoc;
        private dynamic _acadUtil;

        public ArchCadInteropService()
        {
            ConnectToAutoCAD();
        }

        private void ConnectToAutoCAD()
        {
            try
            {
                _acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                _acadDoc = _acadApp.ActiveDocument;
                _acadUtil = _acadDoc.Utility;
            }
            catch (Exception)
            {
                throw new Exception("Could not connect to AutoCAD. Please ensure AutoCAD is open.");
            }
        }

        private XYZ CadToRevit(double xMm, double yMm, double elevFeet = 0)
        {
            return Antigravity.Core.Services.CoordinateService.CadToRevit(xMm, yMm, elevFeet);
        }

        public Dictionary<string, List<BlockInfo>> SelectAndParseBlocks(string prompt)
        {
            var dict = new Dictionary<string, List<BlockInfo>>();
            try
            {
                _acadApp.Visible = true;
                string ssetName = "BlockSel_" + DateTime.Now.Ticks;
                dynamic ssets = _acadDoc.SelectionSets;
                dynamic sset = ssets.Add(ssetName);

                _acadUtil.Prompt($"\n{prompt} ");
                sset.SelectOnScreen();

                for (int i = 0; i < sset.Count; i++)
                {
                    dynamic entity = sset.Item(i);
                    string objName = entity.ObjectName.ToString();
                    
                    if (objName == "AcDbBlockReference")
                    {
                        string blockName = entity.Name.ToString();
                        string layerName = entity.Layer.ToString();
                        double rotation = (double)entity.Rotation;
                        dynamic insertionPt = entity.InsertionPoint;
                        
                        double x = (double)insertionPt[0];
                        double y = (double)insertionPt[1];

                        double cx = x, cy = y;
                        try
                        {
                            object minExt, maxExt;
                            entity.GetBoundingBox(out minExt, out maxExt);
                            double[] min = (double[])minExt;
                            double[] max = (double[])maxExt;
                            cx = (min[0] + max[0]) / 2.0;
                            cy = (min[1] + max[1]) / 2.0;
                        }
                        catch { }

                        double scaleX = 1.0, scaleY = 1.0;
                        try
                        {
                            scaleX = (double)entity.XScaleFactor;
                            scaleY = (double)entity.YScaleFactor;
                        }
                        catch { }

                        bool isFlippedX = false;
                        bool isFlippedY = false;
                        try
                        {
                            if ((bool)entity.IsDynamicBlock)
                            {
                                dynamic props = entity.GetDynamicBlockProperties();
                                for (int pIdx = 0; pIdx < props.Length; pIdx++)
                                {
                                    dynamic p = props[pIdx];
                                    string pName = p.PropertyName.ToString().ToLower();
                                    if (pName.Contains("flip") || pName.Contains("lật"))
                                    {
                                        int val = Convert.ToInt32(p.Value);
                                        if (val == 1)
                                        {
                                            // Depending on how it's named, we guess.
                                            // For simplicity, we just toggle isFlippedX if there's a flip.
                                            if (pName.Contains("y") || pName.Contains("dọc")) isFlippedY = true;
                                            else isFlippedX = true;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }

                        string handle = "";
                        try { handle = entity.Handle.ToString(); } catch { }

                        // EXTRACT GEOMETRY VECTOR
                        double fvx = 0, fvy = 0;
                        try
                        {
                            var facingVec = GetFacingVectorFromBlockGeometry(entity, _acadDoc);
                            if (facingVec != null)
                            {
                                fvx = facingVec[0];
                                fvy = facingVec[1];
                            }
                        }
                        catch { }

                        var blockInfo = new BlockInfo(x, y, rotation, blockName, layerName, cx, cy, scaleX, scaleY, isFlippedX, isFlippedY, handle, fvx, fvy);

                        if (!dict.ContainsKey(blockName))
                            dict[blockName] = new List<BlockInfo>();
                            
                        dict[blockName].Add(blockInfo);
                    }
                }
                sset.Delete();
            }
            catch (Exception ex)
            {
                throw new Exception("Error selecting blocks: " + ex.Message);
            }
            return dict;
        }

        public CombinedScanResult SelectAndParseCombined(string prompt)
        {
            var result = new CombinedScanResult();
            var dictWalls = new Dictionary<string, List<WallData>>();
            var blocks = new List<BlockInfo>();
            
            var hatchHandles = new List<string>();
            var hatchEntities = new List<dynamic>();

            try
            {
                _acadApp.Visible = true;
                string ssetName = "CombSel_" + DateTime.Now.Ticks;
                dynamic ssets = _acadDoc.SelectionSets;
                dynamic sset = ssets.Add(ssetName);

                _acadUtil.Prompt($"\n{prompt} ");
                sset.SelectOnScreen();

                for (int i = 0; i < sset.Count; i++)
                {
                    dynamic entity = sset.Item(i);
                    string objName = "";
                    try { objName = entity.ObjectName.ToString(); } catch { }
                    
                    if (objName == "AcDbBlockReference")
                    {
                        try
                        {
                            string blockName = entity.Name.ToString();
                            string layerName = entity.Layer.ToString();
                            double rotation = (double)entity.Rotation;
                            dynamic insertionPt = entity.InsertionPoint;
                            
                            double x = (double)insertionPt[0];
                            double y = (double)insertionPt[1];

                            double cx = x, cy = y;
                            try
                            {
                                object minExt, maxExt;
                                entity.GetBoundingBox(out minExt, out maxExt);
                                double[] min = (double[])minExt;
                                double[] max = (double[])maxExt;
                                cx = (min[0] + max[0]) / 2.0;
                                cy = (min[1] + max[1]) / 2.0;
                            }
                            catch { }

                            double scaleX = 1.0, scaleY = 1.0;
                            try
                            {
                                scaleX = (double)entity.XScaleFactor;
                                scaleY = (double)entity.YScaleFactor;
                            }
                            catch { }

                            bool isFlippedX = false;
                            bool isFlippedY = false;
                            try
                            {
                                if ((bool)entity.IsDynamicBlock)
                                {
                                    dynamic props = entity.GetDynamicBlockProperties();
                                    for (int pIdx = 0; pIdx < props.Length; pIdx++)
                                    {
                                        dynamic p = props[pIdx];
                                        string pName = p.PropertyName.ToString().ToLower();
                                        if (pName.Contains("flip") || pName.Contains("lật"))
                                        {
                                            int val = Convert.ToInt32(p.Value);
                                            if (val == 1)
                                            {
                                                if (pName.Contains("y") || pName.Contains("dọc")) isFlippedY = true;
                                                else isFlippedX = true;
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }

                            string handle = "";
                            try { handle = entity.Handle.ToString(); } catch { }

                            // EXTRACT GEOMETRY VECTOR
                            double fvx = 0, fvy = 0;
                            try
                            {
                                var facingVec = GetFacingVectorFromBlockGeometry(entity, _acadDoc);
                                if (facingVec != null)
                                {
                                    fvx = facingVec[0];
                                    fvy = facingVec[1];
                                }
                            }
                            catch { }

                            blocks.Add(new BlockInfo(x, y, rotation, blockName, layerName, cx, cy, scaleX, scaleY, isFlippedX, isFlippedY, handle, fvx, fvy));
                        }
                        catch { }
                        continue;
                    }

                    string keyName = entity.Layer.ToString();
                    string entityHandle = "";
                    try { entityHandle = entity.Handle.ToString(); } catch { }

                    if (objName == "AcDbHatch")
                    {
                        // We will collect hatch handles and process them later
                        if (!string.IsNullOrEmpty(entityHandle))
                        {
                            hatchHandles.Add(entityHandle);
                            hatchEntities.Add(entity);
                            continue; // Process later
                        }
                    }
                    
                    // Box logic
                    object minPtObj = null;
                    object maxPtObj = null;
                    try { entity.GetBoundingBox(out minPtObj, out maxPtObj); } catch { continue; }
                    
                    if (minPtObj == null || maxPtObj == null) continue;

                    double[] minPt = (double[])minPtObj;
                    double[] maxPt = (double[])maxPtObj;

                    double minX = minPt[0];
                    double minY = minPt[1];
                    double maxX = maxPt[0];
                    double maxY = maxPt[1];
                    
                    double widthMm = Math.Abs(maxX - minX);
                    double heightMm = Math.Abs(maxY - minY);
                    
                    double thicknessMm = Math.Min(widthMm, heightMm);
                    int roundedThickness = (int)Math.Round(thicknessMm);
                    string finalKey = $"{keyName}|{roundedThickness}";
                    
                    XYZ pt1, pt2;
                    if (widthMm > heightMm)
                    {
                        double midY = (minY + maxY) / 2.0;
                        pt1 = CadToRevit(minX, midY);
                        pt2 = CadToRevit(maxX, midY);
                    }
                    else
                    {
                        double midX = (minX + maxX) / 2.0;
                        pt1 = CadToRevit(midX, minY);
                        pt2 = CadToRevit(midX, maxY);
                    }
                    
                    Curve centerLine = null;
                    if (pt1.DistanceTo(pt2) > 0.003)
                    {
                        centerLine = Line.CreateBound(pt1, pt2);
                    }
                    
                    if (centerLine != null)
                    {
                        if (!dictWalls.ContainsKey(finalKey))
                            dictWalls[finalKey] = new List<WallData>();
                            
                        dictWalls[finalKey].Add(new WallData(new List<Curve>(), thicknessMm, centerLine));
                    }
                }
                
                if (hatchHandles.Count > 0)
                {
                    try
                    {
                        var orchestrator = new HatchScanOrchestrator(_acadDoc);
                        var hatchDetails = orchestrator.GetHatchDetails(hatchHandles);

                        var hashToName = new Dictionary<string, string>();
                        var nameToHash = new Dictionary<string, string>();

                        for (int hIdx = 0; hIdx < hatchEntities.Count; hIdx++)
                        {
                            dynamic entity = hatchEntities[hIdx];
                            string handle = hatchHandles[hIdx];
                            string keyName = entity.Layer.ToString();

                            if (hatchDetails.TryGetValue(handle, out var detail) && (detail.Status == "Extracted" || detail.Status == "Solid"))
                            {
                                if (detail.Status == "Solid")
                                {
                                    keyName = "SOLID";
                                }
                                else if (detail.DefinitionLines != null && detail.DefinitionLines.Count > 0)
                                {
                                    var sigParts = new List<string>();
                                    foreach (var line in detail.DefinitionLines)
                                    {
                                        double fAng = line.AngleRadians + detail.PatternAngleRadians;
                                        while (fAng < 0) fAng += Math.PI;
                                        while (fAng >= Math.PI) fAng -= Math.PI;
                                        double fOffX = line.OffsetX * detail.PatternScale;
                                        double fOffY = line.OffsetY * detail.PatternScale;
                                        sigParts.Add($"{Math.Round(fAng, 1)}_{Math.Round(Math.Abs(fOffX), 1)}_{Math.Round(Math.Abs(fOffY), 1)}");
                                    }
                                    // sigParts.Add($"Layer:{detail.Layer}_Color:{detail.Color}"); // Removed per user request to ignore color/layer for grouping
                                    sigParts.Sort();
                                    string geomHash = string.Join("-", sigParts).GetHashCode().ToString("X");
                                    
                                    if (!hashToName.ContainsKey(geomHash))
                                    {
                                        string pName = string.IsNullOrEmpty(detail.PatternName) ? "Hatch" : detail.PatternName;
                                        if (pName.StartsWith("_")) pName = pName.Substring(1);
                                        double angDeg = Math.Round(detail.PatternAngleRadians * 180.0 / Math.PI, 1);
                                        double scl = Math.Round(detail.PatternScale, 2);
                                        string colorInfo = string.IsNullOrEmpty(detail.Color) ? "" : $" C={detail.Color}";
                                        string layerInfo = string.IsNullOrEmpty(detail.Layer) ? "" : $" L={detail.Layer}";
                                        string desiredName = $"{pName} (S={scl}, A={angDeg} deg{colorInfo}{layerInfo})";
                                        
                                        if (nameToHash.ContainsKey(desiredName) && nameToHash[desiredName] != geomHash)
                                        {
                                            desiredName = $"{desiredName} [{geomHash.Substring(0, Math.Min(4, geomHash.Length))}]";
                                        }
                                        hashToName[geomHash] = desiredName;
                                        nameToHash[desiredName] = geomHash;
                                    }
                                    keyName = hashToName[geomHash];
                                }
                            }

                            object minPtObj = null, maxPtObj = null;
                            try { entity.GetBoundingBox(out minPtObj, out maxPtObj); } catch { continue; }
                            
                            if (minPtObj == null || maxPtObj == null) continue;

                            double[] minPt = (double[])minPtObj;
                            double[] maxPt = (double[])maxPtObj;

                            double minX = minPt[0], minY = minPt[1], maxX = maxPt[0], maxY = maxPt[1];
                            double widthMm = Math.Abs(maxX - minX);
                            double heightMm = Math.Abs(maxY - minY);
                            double thicknessMm = Math.Min(widthMm, heightMm);
                            int roundedThickness = (int)Math.Round(thicknessMm);
                            string finalKey = $"{keyName}|{roundedThickness}";
                            
                            XYZ pt1, pt2;
                            if (widthMm > heightMm)
                            {
                                double midY = (minY + maxY) / 2.0;
                                pt1 = CadToRevit(minX, midY);
                                pt2 = CadToRevit(maxX, midY);
                            }
                            else
                            {
                                double midX = (minX + maxX) / 2.0;
                                pt1 = CadToRevit(midX, minY);
                                pt2 = CadToRevit(midX, maxY);
                            }
                            
                            Curve centerLine = null;
                            if (pt1.DistanceTo(pt2) > 0.003)
                                centerLine = Line.CreateBound(pt1, pt2);
                            
                            if (centerLine != null)
                            {
                                if (!dictWalls.ContainsKey(finalKey))
                                    dictWalls[finalKey] = new List<WallData>();
                                    
                                dictWalls[finalKey].Add(new WallData(new List<Curve>(), thicknessMm, centerLine));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Orchestrator failed: {ex.Message}");
                    }
                }

                var stitcher = new WallCenterlineStitcher();
                foreach (var kvp in dictWalls)
                {
                    result.Walls[kvp.Key] = stitcher.Stitch(kvp.Value, blocks);
                }

                // Group blocks by name for Doors
                foreach (var block in blocks)
                {
                    if (!result.Doors.ContainsKey(block.BlockName))
                        result.Doors[block.BlockName] = new List<BlockInfo>();
                    result.Doors[block.BlockName].Add(block);
                }

                sset.Delete();
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("Error parsing combined selection: " + ex.Message);
            }
        }

        public Dictionary<string, List<WallData>> SelectAndParseByLayer(string prompt)
        {
            var dict = new Dictionary<string, List<WallData>>();
            var blocks = new List<BlockInfo>();

            var hatchHandles = new List<string>();
            var hatchEntities = new List<dynamic>();

            try
            {
                _acadApp.Visible = true;
                string ssetName = "WallSel_" + DateTime.Now.Ticks;
                dynamic ssets = _acadDoc.SelectionSets;
                dynamic sset = ssets.Add(ssetName);

                _acadUtil.Prompt($"\n{prompt} ");
                sset.SelectOnScreen();

                for (int i = 0; i < sset.Count; i++)
                {
                    dynamic entity = sset.Item(i);
                    string objName = "";
                    try { objName = entity.ObjectName.ToString(); } catch { }
                    
                    if (objName == "AcDbBlockReference")
                    {
                        try
                        {
                            string blockName = entity.Name.ToString();
                            string layerName = entity.Layer.ToString();
                            double rotation = (double)entity.Rotation;
                            dynamic insertionPt = entity.InsertionPoint;
                            
                            double x = (double)insertionPt[0];
                            double y = (double)insertionPt[1];

                            double cx = x, cy = y;
                            try
                            {
                                object minExt, maxExt;
                                entity.GetBoundingBox(out minExt, out maxExt);
                                double[] min = (double[])minExt;
                                double[] max = (double[])maxExt;
                                cx = (min[0] + max[0]) / 2.0;
                                cy = (min[1] + max[1]) / 2.0;
                            }
                            catch { }

                            double scaleX = 1.0, scaleY = 1.0;
                            try
                            {
                                scaleX = (double)entity.XScaleFactor;
                                scaleY = (double)entity.YScaleFactor;
                            }
                            catch { }

                            bool isFlippedX = false;
                            bool isFlippedY = false;
                            try
                            {
                                if ((bool)entity.IsDynamicBlock)
                                {
                                    dynamic props = entity.GetDynamicBlockProperties();
                                    for (int pIdx = 0; pIdx < props.Length; pIdx++)
                                    {
                                        dynamic p = props[pIdx];
                                        string pName = p.PropertyName.ToString().ToLower();
                                        if (pName.Contains("flip") || pName.Contains("lật"))
                                        {
                                            int val = Convert.ToInt32(p.Value);
                                            if (val == 1)
                                            {
                                                if (pName.Contains("y") || pName.Contains("dọc")) isFlippedY = true;
                                                else isFlippedX = true;
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }

                            string handle = "";
                            try { handle = entity.Handle.ToString(); } catch { }

                            // EXTRACT GEOMETRY VECTOR
                            double fvx = 0, fvy = 0;
                            try
                            {
                                var facingVec = GetFacingVectorFromBlockGeometry(entity, _acadDoc);
                                if (facingVec != null)
                                {
                                    fvx = facingVec[0];
                                    fvy = facingVec[1];
                                }
                            }
                            catch { }

                            blocks.Add(new BlockInfo(x, y, rotation, blockName, layerName, cx, cy, scaleX, scaleY, isFlippedX, isFlippedY, handle, fvx, fvy));
                        }
                        catch { }
                        continue;
                    }

                    string keyName = entity.Layer.ToString();
                    string entityHandle = "";
                    try { entityHandle = entity.Handle.ToString(); } catch { }

                    if (objName == "AcDbHatch")
                    {
                        if (!string.IsNullOrEmpty(entityHandle))
                        {
                            hatchHandles.Add(entityHandle);
                            hatchEntities.Add(entity);
                            continue; // Process later
                        }
                    }
                    
                    // For MVP: We assume the layer has Hatches, Lines, Polylines representing walls.
                    // For walls, we need bounding box or parallel lines to find thickness and centerline.
                    // Let's extract the bounding box of the entity as a simple WallData proxy for now.
                    object minPtObj = null;
                    object maxPtObj = null;
                    entity.GetBoundingBox(out minPtObj, out maxPtObj);
                    
                    double[] minPt = (double[])minPtObj;
                    double[] maxPt = (double[])maxPtObj;

                    double minX = minPt[0];
                    double minY = minPt[1];
                    double maxX = maxPt[0];
                    double maxY = maxPt[1];
                    
                    double widthMm = Math.Abs(maxX - minX);
                    double heightMm = Math.Abs(maxY - minY);
                    
                    // Simple heuristic: shortest dimension is thickness, longest is length.
                    double thicknessMm = Math.Min(widthMm, heightMm);
                    int roundedThickness = (int)Math.Round(thicknessMm);
                    string finalKey = $"{keyName}|{roundedThickness}";
                    
                    XYZ pt1, pt2;
                    if (widthMm > heightMm)
                    {
                        double midY = (minY + maxY) / 2.0;
                        pt1 = CadToRevit(minX, midY);
                        pt2 = CadToRevit(maxX, midY);
                    }
                    else
                    {
                        double midX = (minX + maxX) / 2.0;
                        pt1 = CadToRevit(midX, minY);
                        pt2 = CadToRevit(midX, maxY);
                    }
                    
                    Curve centerLine = null;
                    if (pt1.DistanceTo(pt2) > 0.003)
                    {
                        centerLine = Line.CreateBound(pt1, pt2);
                    }
                    
                    if (centerLine != null)
                    {
                        if (!dict.ContainsKey(finalKey))
                            dict[finalKey] = new List<WallData>();
                            
                        dict[finalKey].Add(new WallData(new List<Curve>(), thicknessMm, centerLine));
                    }
                }
                
                if (hatchHandles.Count > 0)
                {
                    var hashToName2 = new Dictionary<string, string>();
                    var nameToHash2 = new Dictionary<string, string>();
                    try
                    {
                        var orchestrator = new HatchScanOrchestrator(_acadDoc);
                        var hatchDetails = orchestrator.GetHatchDetails(hatchHandles);

                        for (int hIdx = 0; hIdx < hatchEntities.Count; hIdx++)
                        {
                            dynamic entity = hatchEntities[hIdx];
                            string handle = hatchHandles[hIdx];
                            string keyName = entity.Layer.ToString();

                            if (hatchDetails.TryGetValue(handle, out var detail) && (detail.Status == "Extracted" || detail.Status == "Solid"))
                            {
                                if (detail.Status == "Solid")
                                {
                                    keyName = "SOLID";
                                }
                                else if (detail.DefinitionLines != null && detail.DefinitionLines.Count > 0)
                                {
                                    var sigParts = new List<string>();
                                    foreach (var line in detail.DefinitionLines)
                                    {
                                        double fAng = line.AngleRadians + detail.PatternAngleRadians;
                                        while (fAng < 0) fAng += Math.PI;
                                        while (fAng >= Math.PI) fAng -= Math.PI;
                                        double fOffX = line.OffsetX * detail.PatternScale;
                                        double fOffY = line.OffsetY * detail.PatternScale;
                                        sigParts.Add($"{Math.Round(fAng, 1)}_{Math.Round(Math.Abs(fOffX), 1)}_{Math.Round(Math.Abs(fOffY), 1)}");
                                    }
                                    // sigParts.Add($"Layer:{detail.Layer}_Color:{detail.Color}"); // Removed per user request to ignore color/layer for grouping
                                    sigParts.Sort();
                                    string geomHash = string.Join("-", sigParts).GetHashCode().ToString("X");
                                    
                                    if (!hashToName2.ContainsKey(geomHash))
                                    {
                                        string pName = string.IsNullOrEmpty(detail.PatternName) ? "Hatch" : detail.PatternName;
                                        if (pName.StartsWith("_")) pName = pName.Substring(1);
                                        double angDeg = Math.Round(detail.PatternAngleRadians * 180.0 / Math.PI, 1);
                                        double scl = Math.Round(detail.PatternScale, 2);
                                        string colorInfo = string.IsNullOrEmpty(detail.Color) ? "" : $" C={detail.Color}";
                                        string layerInfo = string.IsNullOrEmpty(detail.Layer) ? "" : $" L={detail.Layer}";
                                        string desiredName = $"{pName} (S={scl}, A={angDeg} deg{colorInfo}{layerInfo})";
                                        
                                        if (nameToHash2.ContainsKey(desiredName) && nameToHash2[desiredName] != geomHash)
                                        {
                                            desiredName = $"{desiredName} [{geomHash.Substring(0, Math.Min(4, geomHash.Length))}]";
                                        }
                                        hashToName2[geomHash] = desiredName;
                                        nameToHash2[desiredName] = geomHash;
                                    }
                                    keyName = hashToName2[geomHash];
                                }
                            }

                            object minPtObj = null, maxPtObj = null;
                            try { entity.GetBoundingBox(out minPtObj, out maxPtObj); } catch { continue; }
                            
                            if (minPtObj == null || maxPtObj == null) continue;

                            double[] minPt = (double[])minPtObj;
                            double[] maxPt = (double[])maxPtObj;

                            double minX = minPt[0], minY = minPt[1], maxX = maxPt[0], maxY = maxPt[1];
                            double widthMm = Math.Abs(maxX - minX);
                            double heightMm = Math.Abs(maxY - minY);
                            double thicknessMm = Math.Min(widthMm, heightMm);
                            int roundedThickness = (int)Math.Round(thicknessMm);
                            string finalKey = $"{keyName}|{roundedThickness}";
                            
                            XYZ pt1, pt2;
                            if (widthMm > heightMm)
                            {
                                double midY = (minY + maxY) / 2.0;
                                pt1 = CadToRevit(minX, midY);
                                pt2 = CadToRevit(maxX, midY);
                            }
                            else
                            {
                                double midX = (minX + maxX) / 2.0;
                                pt1 = CadToRevit(midX, minY);
                                pt2 = CadToRevit(midX, maxY);
                            }
                            
                            Curve centerLine = null;
                            if (pt1.DistanceTo(pt2) > 0.003)
                                centerLine = Line.CreateBound(pt1, pt2);
                            
                            if (centerLine != null)
                            {
                                if (!dict.ContainsKey(finalKey))
                                    dict.Add(finalKey, new List<WallData>());
                                    
                                dict[finalKey].Add(new WallData(new List<Curve>(), thicknessMm, centerLine));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Orchestrator failed: {ex.Message}");
                    }
                }

                var stitcher = new WallCenterlineStitcher();
                var mergedDict = new Dictionary<string, List<WallData>>();
                foreach (var kvp in dict)
                {
                    mergedDict[kvp.Key] = stitcher.Stitch(kvp.Value, blocks);
                }

                sset.Delete();
                return mergedDict;
            }
            catch (Exception ex)
            {
                throw new Exception("Error selecting walls by layer: " + ex.Message);
            }
        }

        /// <summary>
        /// Marks the given CAD entities red by their handle strings.
        /// Call this AFTER the Revit transaction to highlight ambiguous door placements.
        /// </summary>
        public void MarkEntitiesRed(IEnumerable<string> handles)
        {
            if (handles == null) return;
            var handleSet = new HashSet<string>(handles);
            try
            {
                dynamic modelSpace = _acadDoc.ModelSpace;
                for (int i = 0; i < modelSpace.Count; i++)
                {
                    dynamic e = modelSpace.Item(i);
                    try
                    {
                        string h = e.Handle.ToString();
                        if (handleSet.Contains(h))
                        {
                            e.Color = 1; // ACI 1 = Red
                        }
                    }
                    catch { }
                }
                _acadDoc.Regen();
            }
            catch { }
        }

        /// <summary>
        /// Reads internal block geometry (ARCs primarily) to determine the swing direction.
        /// Returns [FacingX, FacingY] in World space.
        /// </summary>
        private double[] GetFacingVectorFromBlockGeometry(dynamic blockRef, dynamic acadDoc)
        {
            try
            {
                string blockName = blockRef.Name.ToString();
                dynamic blockDef = acadDoc.Database.Blocks.Item(blockName);

                double[] bestVecLocal = null;
                double maxArcLength = -1;

                // 1. Look for ARCs
                for (int i = 0; i < blockDef.Count; i++)
                {
                    dynamic subEnt = blockDef.Item(i);
                    string objName = "";
                    try { objName = subEnt.ObjectName.ToString(); } catch { }

                    if (objName == "AcDbArc")
                    {
                        double radius = (double)subEnt.Radius;
                        double startAngle = (double)subEnt.StartAngle; // Radian
                        double endAngle = (double)subEnt.EndAngle;     // Radian
                        dynamic center = subEnt.Center;

                        // Calculate arc length
                        double angleDiff = endAngle - startAngle;
                        if (angleDiff < 0) angleDiff += 2 * Math.PI;
                        double arcLen = radius * angleDiff;

                        if (arcLen > maxArcLength)
                        {
                            maxArcLength = arcLen;
                            
                            // Midpoint angle
                            double midAngle = startAngle + angleDiff / 2.0;
                            
                            // Vector from center to midpoint (local space)
                            double mx = Math.Cos(midAngle) * radius;
                            double my = Math.Sin(midAngle) * radius;
                            
                            bestVecLocal = new double[] { mx, my };
                        }
                    }
                }

                // If no ARC found, we could look for LINE, but the user confirmed "Cứ lấy theo hướng cung tròn cửa là được"
                // so we stick to ARC priority.

                if (bestVecLocal != null)
                {
                    // Transform local vector to world vector using block reference transform
                    // Local vector is a directional vector, so we only apply rotation and scale, not translation (insertion point).
                    // Actually, to be precise, the block's rotation and flip affect the vector.
                    
                    double rotation = (double)blockRef.Rotation; // Radian
                    double scaleX = 1.0, scaleY = 1.0;
                    try { scaleX = (double)blockRef.XScaleFactor; } catch { }
                    try { scaleY = (double)blockRef.YScaleFactor; } catch { }
                    
                    // Note: Flip is usually handled by negative scale in AutoCAD.
                    double lx = bestVecLocal[0] * scaleX;
                    double ly = bestVecLocal[1] * scaleY;
                    
                    double cosR = Math.Cos(rotation);
                    double sinR = Math.Sin(rotation);
                    
                    double wx = lx * cosR - ly * sinR;
                    double wy = lx * sinR + ly * cosR;
                    
                    return new double[] { wx, wy };
                }
            }
            catch { }
            return null;
        }
    }
}
