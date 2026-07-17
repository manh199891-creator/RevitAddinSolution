using Antigravity.DrawWalls.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Antigravity.DrawWalls.Services
{
    public class CadInteropService : IDisposable
    {
        private dynamic _acadApp;
        private dynamic _acadDoc;
        private bool _connected = false;

        public bool Connect()
        {
            try
            {
                _acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                _acadDoc = _acadApp.ActiveDocument;
                _connected = true;
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Không thể kết nối với AutoCAD. Đảm bảo AutoCAD đang mở.", ex);
            }
        }

        public double[] GetPoint(string prompt)
        {
            if (!_connected) Connect();
            try
            {
                _acadApp.Visible = true;
                dynamic utility = _acadDoc.Utility;
                var pt = utility.GetPoint(Type.Missing, "\n" + prompt + ": ");
                return pt as double[];
            }
            catch { throw new Exception("Hủy chọn điểm."); }
        }

        public void SetOriginFromRevitPoint(Autodesk.Revit.DB.XYZ revitOriginFeet)
        {
            try
            {
                _acadApp.Visible = true;
                dynamic utility = _acadDoc.Utility;
                utility.Prompt("\nClick the corresponding origin point on the AutoCAD drawing: ");
                dynamic cadPt = utility.GetPoint(Type.Missing, "\nSelect CAD origin: ");
                
                double cadX = (double)cadPt[0]; // mm
                double cadY = (double)cadPt[1]; // mm

                Autodesk.Revit.DB.XYZ offset = new Autodesk.Revit.DB.XYZ(
                    revitOriginFeet.X - cadX / 304.8,
                    revitOriginFeet.Y - cadY / 304.8,
                    0);
                    
                Antigravity.Core.Services.CoordinateService.SetOriginOffset(offset);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting origin point from CAD: " + ex.Message);
            }
        }

        public List<WallData> SelectWalls()
        {
            if (!_connected) Connect();
            List<WallData> walls = new List<WallData>();

            try
            {
                dynamic utility = _acadDoc.Utility;
                dynamic ssets = _acadDoc.SelectionSets;

                string ssetName = "WallsSet_" + DateTime.Now.Ticks;
                dynamic sset = ssets.Add(ssetName);

                utility.Prompt("\nQuét chọn các đối tượng vách (Hatch, Polyline, Block)... ");
                sset.SelectOnScreen();

                for (int i = 0; i < sset.Count; i++)
                {
                    dynamic entity = sset.Item(i);
                    WallData data = ParseWallEntity(entity);
                    if (data != null)
                    {
                        data.Handle = entity.Handle;
                        walls.Add(data);
                    }
                }

                sset.Delete();
                return walls;
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi khi chọn đối tượng CAD: " + ex.Message);
            }
        }

        private WallData ParseWallEntity(dynamic entity)
        {
            string objName = entity.ObjectName;
            WallData data = new WallData();

            try
            {
                object minExt, maxExt;
                entity.GetBoundingBox(out minExt, out maxExt);
                double[] min = minExt as double[];
                double[] max = maxExt as double[];

                if (min == null || max == null) return null;

                double dx = Math.Abs(max[0] - min[0]);
                double dy = Math.Abs(max[1] - min[1]);

                // Simple logic: Wall thickness is the smaller dimension of a rectangle
                // This is a simplification, but often true for standard wall segments.
                // For complex polylines, we'd need more sophisticated path detection.
                if (dx > dy)
                {
                    // Horizontal wall
                    data.Thickness = dy;
                    data.StartX = min[0];
                    data.StartY = min[1] + dy / 2.0;
                    data.EndX = max[0];
                    data.EndY = min[1] + dy / 2.0;
                }
                else
                {
                    // Vertical wall
                    data.Thickness = dx;
                    data.StartX = min[0] + dx / 2.0;
                    data.StartY = min[1];
                    data.EndX = min[0] + dx / 2.0;
                    data.EndY = max[1];
                }

                // If it's a polyline, we could store more points for "Trace by Polyline"
                if (objName.Contains("Polyline"))
                {
                    // Extract coordinates if needed for complex shapes
                    // For now, bounding box centerline is used for standard walls.
                }

                return data;
            }
            catch { return null; }
        }

        public void DrawRedLine(WallData wall)
        {
            if (!_connected) Connect();
            try
            {
                double[] start = new double[] { wall.StartX, wall.StartY, 0 };
                double[] end = new double[] { wall.EndX, wall.EndY, 0 };

                dynamic modelSpace = _acadDoc.ModelSpace;
                dynamic line = modelSpace.AddLine(start, end);
                
                // Color index 1 is Red in AutoCAD
                line.Color = 1; 
                line.Update();
            }
            catch { /* Ignore draw errors */ }
        }

        public void Dispose()
        {
            // Clean up COM objects if necessary
        }
    }
}
