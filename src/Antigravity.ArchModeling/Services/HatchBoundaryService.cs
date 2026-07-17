using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Autodesk.Revit.DB;
using Antigravity.ArchModeling.Models;

namespace Antigravity.ArchModeling.Services
{
    public class HatchBoundaryService
    {
        private readonly Antigravity.DrawFloors.Services.CadInteropService _drawFloorsCadService;

        public HatchBoundaryService()
        {
            _drawFloorsCadService = new Antigravity.DrawFloors.Services.CadInteropService();
        }

        public HatchScanResult GetHatchBoundaries()
        {
            var result = new HatchScanResult();

            // ── Kết nối AutoCAD ─────────────────────────────────────────────────
            dynamic acadApp, acadDoc, acadUtil;
            try
            {
                acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                acadDoc = acadApp.ActiveDocument;
                acadUtil = acadDoc.Utility;
            }
            catch
            {
                throw new Exception("Could not connect to AutoCAD. Please ensure AutoCAD is open.");
            }

            // ── Cho user quét chọn Hatch ────────────────────────────────────────
            var hatchHandles  = new List<string>();
            var hatchEntities = new List<dynamic>();

            acadApp.Visible = true;
            string ssetName = "HatchSelFloor_" + DateTime.Now.Ticks;
            dynamic ssets = acadDoc.SelectionSets;
            dynamic sset  = ssets.Add(ssetName);

            acadUtil.Prompt("\nSelect Hatch regions for automatic floor/ceiling creation... ");
            sset.SelectOnScreen();

            for (int i = 0; i < sset.Count; i++)
            {
                dynamic entity  = sset.Item(i);
                string  objName = "";
                try { objName = entity.ObjectName.ToString(); } catch { }

                if (objName == "AcDbHatch")
                {
                    string handle = "";
                    try { handle = entity.Handle.ToString(); } catch { }
                    if (!string.IsNullOrEmpty(handle))
                    {
                        hatchHandles.Add(handle);
                        hatchEntities.Add(entity);
                    }
                }
            }
            try { sset.Delete(); } catch { }

            if (hatchHandles.Count == 0)
                return result;

            // ── Gọi ngầm AutoCAD qua Orchestrator để lấy Signature ─────────────
            Dictionary<string, Antigravity.HatchPatterns.Contracts.HatchBridgeResponseItem> hatchDetails = null;
            try
            {
                var orchestrator = new HatchScanOrchestrator(acadDoc);
                hatchDetails = orchestrator.GetHatchDetails(hatchHandles);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HatchBoundaryService] Orchestrator failed: {ex.Message}");
                // Degraded mode: tiếp tục, dùng tên layer làm key
                try
                {
                    string debugPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Antigravity", "debug_hatch_sig.log");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(debugPath));
                    string debugLine = $"[{DateTime.Now:HH:mm:ss}] Orchestrator FAILED: {ex.Message}\r\n";
                    System.IO.File.AppendAllText(debugPath, debugLine);
                }
                catch { }
            }

            // ── Tính key (Signature hoặc fallback Layer) và lấy CurveLoop ───────
            var hashToName = new Dictionary<string, string>();
            var nameToHash = new Dictionary<string, string>();

            for (int hIdx = 0; hIdx < hatchEntities.Count; hIdx++)
            {
                dynamic entity = hatchEntities[hIdx];
                string  handle = hatchHandles[hIdx];

                string keyName = "";
                try 
                { 
                    string pName = entity.PatternName;
                    if (pName.StartsWith("_")) pName = pName.Substring(1);
                    keyName = pName;
                    
                    try
                    {
                        double scale = Math.Round((double)entity.PatternScale, 2);
                        double angle = Math.Round((double)entity.PatternAngle * 180.0 / Math.PI, 1);
                        keyName = $"{keyName} (S={scale}, A={angle} deg)";
                    }
                    catch { }
                } 
                catch 
                { 
                    keyName = "UnknownPattern"; 
                }

                // Build PatternInfo
                var patternInfo = new HatchPatternInfo();

                if (hatchDetails != null && hatchDetails.TryGetValue(handle, out var detail))
                {
                    if (detail.Status == "Solid")
                    {
                        keyName = "SOLID";
                    }
                    else if (detail.Status == "Extracted"
                             && detail.DefinitionLines != null
                             && detail.DefinitionLines.Count > 0)
                    {
                        var sigParts = new List<string>();
                        
                        // Handle double hatch for UserDefined if AutoCAD didn't return the 2nd line
                        var lines = new List<Antigravity.HatchPatterns.Contracts.RawPatternLine>(detail.DefinitionLines);
                        if (detail.PatternType == "UserDefined" && detail.PatternDouble && lines.Count == 1)
                        {
                            var perpLine = new Antigravity.HatchPatterns.Contracts.RawPatternLine
                            {
                                AngleRadians = lines[0].AngleRadians + Math.PI / 2,
                                BaseX = lines[0].BaseX,
                                BaseY = lines[0].BaseY,
                                OffsetX = lines[0].OffsetY,
                                OffsetY = lines[0].OffsetX
                            };
                            lines.Add(perpLine);
                        }

                        foreach (var line in lines)
                        {
                            double finalAngleRad = line.AngleRadians + detail.PatternAngleRadians;
                            while (finalAngleRad < 0) finalAngleRad += Math.PI;
                            while (finalAngleRad >= Math.PI) finalAngleRad -= Math.PI;

                            double finalOffsetX = line.OffsetX * detail.PatternScale;
                            double finalOffsetY = line.OffsetY * detail.PatternScale;

                            // Rotate and scale the base point
                            double cosA = Math.Cos(detail.PatternAngleRadians);
                            double sinA = Math.Sin(detail.PatternAngleRadians);
                            double finalBaseX = (line.BaseX * cosA - line.BaseY * sinA) * detail.PatternScale;
                            double finalBaseY = (line.BaseX * sinA + line.BaseY * cosA) * detail.PatternScale;

                            // Scale the dash lengths
                            var finalDashes = new List<double>();
                            if (line.DashLengths != null)
                            {
                                foreach (var d in line.DashLengths)
                                {
                                    finalDashes.Add(d * detail.PatternScale);
                                }
                            }

                            // Round to 1 decimal place and use absolute values for signature stability
                            // (since shifting in negative direction is geometrically identical for infinite grids)
                            double sigAngle = Math.Round(finalAngleRad * 180.0 / Math.PI, 1);
                            double sigOffsetX = Math.Round(Math.Abs(finalOffsetX), 1);
                            double sigOffsetY = Math.Round(Math.Abs(finalOffsetY), 1);

                            sigParts.Add($"{sigAngle}_{sigOffsetX}_{sigOffsetY}");

                            patternInfo.Lines.Add(new HatchLineDefinition
                            {
                                AngleDegrees  = sigAngle,
                                BaseXMm       = finalBaseX,
                                BaseYMm       = finalBaseY,
                                OffsetXMm     = finalOffsetX,
                                OffsetYMm     = finalOffsetY,
                                DashLengthsMm = finalDashes
                            });
                        }
                        
                        // sigParts.Add($"Layer:{detail.Layer}_Color:{detail.Color}"); // Removed per user request to ignore color/layer for grouping
                        sigParts.Sort();
                        string geomHash = string.Join("-", sigParts).GetHashCode().ToString("X");
                        
                        // DEBUG: log signature parts to diagnose grouping issues
                        try
                        {
                            string debugPath = System.IO.Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "Antigravity", "debug_hatch_sig.log");
                            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(debugPath));
                            string debugLine = $"[{DateTime.Now:HH:mm:ss}] Handle={handle} PatternName={detail.PatternName} " +
                                $"Layer={detail.Layer} Color={detail.Color} Lines={detail.DefinitionLines?.Count} " +
                                $"SigParts=[{string.Join(", ", sigParts)}] Hash={geomHash}\r\n";
                            System.IO.File.AppendAllText(debugPath, debugLine);
                        }
                        catch { }
                        
                        if (!hashToName.ContainsKey(geomHash))
                        {
                            string pName = string.IsNullOrEmpty(detail.PatternName) ? "Hatch" : detail.PatternName;
                            if (pName.StartsWith("_")) pName = pName.Substring(1);
                            double angDeg = Math.Round(detail.PatternAngleRadians * 180.0 / Math.PI, 1);
                            double scl = Math.Round(detail.PatternScale, 2);
                            
                            string desiredName = $"{pName} (S={scl}, A={angDeg} deg)";
                            
                            if (nameToHash.ContainsKey(desiredName) && nameToHash[desiredName] != geomHash)
                            {
                                desiredName = $"{desiredName} [{geomHash.Substring(0, Math.Min(4, geomHash.Length))}]";
                            }
                            
                            hashToName[geomHash] = desiredName;
                            nameToHash[desiredName] = geomHash;
                        }
                        
                        keyName = hashToName[geomHash];
                    }
                    else
                    {
                        // Fallback for DefinitionMissing or Unsupported
                        string pName = string.IsNullOrEmpty(detail.PatternName) ? "Hatch" : detail.PatternName;
                        if (pName.StartsWith("_")) pName = pName.Substring(1);
                        keyName = pName;
                        
                        // DEBUG: log missing definition
                        try
                        {
                            string debugPath = System.IO.Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "Antigravity", "debug_hatch_sig.log");
                            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(debugPath));
                            string debugLine = $"[{DateTime.Now:HH:mm:ss}] Handle={handle} PatternName={detail.PatternName} " +
                                $"Layer={detail.Layer} Status={detail.Status} -> Fallback Grouping\r\n";
                            System.IO.File.AppendAllText(debugPath, debugLine);
                        }
                        catch { }
                    }
                }

                if (!result.Boundaries.ContainsKey(keyName))
                {
                    result.Boundaries[keyName]   = new List<IList<CurveLoop>>();
                    result.PatternInfos[keyName] = patternInfo;
                }

                var boundaries = _drawFloorsCadService.ExtractHatchBoundaries(entity);
                if (boundaries != null && boundaries.Count > 0)
                {
                    try
                    {
                        var loops = new List<CurveLoop>();
                        foreach (var b in boundaries)
                            loops.Add(CurveLoop.Create(b));
                        result.Boundaries[keyName].Add(loops);
                    }
                    catch { }
                }
            }

            return result;
        }
    }
}
