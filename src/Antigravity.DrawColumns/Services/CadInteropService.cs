using Antigravity.DrawColumns.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Antigravity.DrawColumns.Services
{
    public class CadInteropService
    {
        private dynamic _acadApp;
        private dynamic _acadDoc;

        public bool Connect()
        {
            try
            {
                _acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                _acadDoc = _acadApp.ActiveDocument;
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Không thể kết nối với AutoCAD. Đảm bảo AutoCAD đang mở.", ex);
            }
        }

        public void SetOriginFromRevitPoint(Autodesk.Revit.DB.XYZ revitOriginFeet)
        {
            try
            {
                _acadApp.Visible = true;
                dynamic utility = _acadDoc.Utility;
                utility.Prompt("\nClick the corresponding origin point on the AutoCAD drawing: ");
                dynamic cadPtUcs = utility.GetPoint(Type.Missing, "\nSelect CAD origin: ");
                dynamic cadPt = utility.TranslateCoordinates(cadPtUcs, 1, 0, false); // UCS to WCS
                
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

        public List<RevitColumnData> GetColumnData()
        {
            List<RevitColumnData> columns = new List<RevitColumnData>();
            
            try
            {
                dynamic utility = _acadDoc.Utility;
                dynamic ssets = _acadDoc.SelectionSets;
                
                string ssetName = "ColumnsSet_" + DateTime.Now.Ticks;
                dynamic sset = null;
                try { sset = ssets.Add(ssetName); }
                catch { sset = ssets.Item(ssetName); }
                
                utility.Prompt("\nQuét chọn các cột (Block, Polyline, Hatch)... ");
                sset.SelectOnScreen();

                for (int i = 0; i < sset.Count; i++)
                {
                    dynamic entity = sset.Item(i);
                    RevitColumnData col = ParseEntity(entity);
                    if (col != null)
                        columns.Add(col);
                }
                
                // Lọc trùng (ưu tiên Block > Polyline > Hatch)
                var finalColumns = new List<RevitColumnData>();
                foreach (var col in columns)
                {
                    var existing = finalColumns.Find(c => Math.Abs(c.X - col.X) < 10.0 && Math.Abs(c.Y - col.Y) < 10.0);
                    if (existing != null)
                    {
                        if (GetEntityPriority(col.EntityType) > GetEntityPriority(existing.EntityType))
                        {
                            finalColumns.Remove(existing);
                            finalColumns.Add(col);
                        }
                    }
                    else
                    {
                        finalColumns.Add(col);
                    }
                }
                
                sset.Delete();
                return finalColumns;
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi khi chọn đối tượng CAD.", ex);
            }
        }

        public List<RevitColumnData> GetHatchData()
        {
            List<RevitColumnData> columns = new List<RevitColumnData>();
            
            try
            {
                dynamic utility = _acadDoc.Utility;
                dynamic ssets = _acadDoc.SelectionSets;
                
                string ssetName = "HatchesSet_" + DateTime.Now.Ticks;
                dynamic sset = null;
                try { sset = ssets.Add(ssetName); }
                catch { sset = ssets.Item(ssetName); }
                
                // Add filter for Hatch only
                short[] filterTypes = new short[] { 0 };
                object[] filterValues = new object[] { "HATCH" };
                
                utility.Prompt("\nQuét chọn các Hatch cột... ");
                sset.SelectOnScreen(filterTypes, filterValues);

                for (int i = 0; i < sset.Count; i++)
                {
                    dynamic entity = sset.Item(i);
                    RevitColumnData col = ParseEntity(entity);
                    if (col != null)
                        columns.Add(col);
                }
                
                sset.Delete();
                return columns;
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi khi chọn đối tượng CAD.", ex);
            }
        }

        public double[][] GetRectanglePoints()
        {
            try
            {
                _acadApp.Visible = true;
                dynamic utility = _acadDoc.Utility;
                var pt1Ucs = utility.GetPoint(Type.Missing, "\nChọn điểm thứ nhất: ");
                var pt2Ucs = utility.GetCorner(pt1Ucs, "\nChọn điểm đối diện: ");
                
                var pt1 = utility.TranslateCoordinates(pt1Ucs, 1, 0, false); // UCS to WCS
                var pt2 = utility.TranslateCoordinates(pt2Ucs, 1, 0, false); // UCS to WCS
                
                return new double[][] { pt1 as double[], pt2 as double[] };
            }
            catch { throw new Exception("Hủy chọn điểm."); }
        }

        public RevitColumnData GetSingleEntity()
        {
            try
            {
                _acadApp.Visible = true;
                dynamic utility = _acadDoc.Utility;
                object entObj;
                object pickPt;
                utility.GetEntity(out entObj, out pickPt, "\nChọn một đối tượng CAD (Polyline, Block, Hatch, Circle): ");
                return ParseEntity(entObj);
            }
            catch { throw new Exception("Hủy chọn đối tượng."); }
        }


        private RevitColumnData ParseEntity(dynamic entity)
        {
            string objName = entity.ObjectName;
            RevitColumnData data = new RevitColumnData();
            data.EntityType = objName;

            try
            {
                if (objName == "AcDbBlockReference")
                {
                    var insPt = entity.InsertionPoint;
                    data.X = insPt[0];
                    data.Y = insPt[1];
                    data.Rotation = entity.Rotation;
                    
                    object minExt, maxExt;
                    entity.GetBoundingBox(out minExt, out maxExt);
                    double[] min = minExt as double[];
                    double[] max = maxExt as double[];
                    if (min != null && max != null)
                    {
                        data.Width = Math.Abs(max[0] - min[0]);
                        data.Height = Math.Abs(max[1] - min[1]);
                    }
                }
                else if (objName == "AcDbPolyline" || objName == "AcDb2dPolyline" || objName == "AcDbHatch" || objName == "AcDbCircle")
                {
                    object minExt, maxExt;
                    entity.GetBoundingBox(out minExt, out maxExt);
                    double[] min = minExt as double[];
                    double[] max = maxExt as double[];
                    if (min != null && max != null)
                    {
                        data.Width = Math.Abs(max[0] - min[0]);
                        data.Height = Math.Abs(max[1] - min[1]);
                        data.X = min[0] + data.Width / 2.0;
                        data.Y = min[1] + data.Height / 2.0;
                        data.Rotation = 0; 

                        if (objName == "AcDbCircle")
                        {
                            data.IsRound = true;
                            data.Radius = entity.Radius;
                        }
                    }
                }
                else
                {
                    return null;
                }

                // Auto-detect name if not already set
                if (string.IsNullOrEmpty(data.ColumnName))
                {
                    data.ColumnName = FindNearbyText(data.X, data.Y, 1500.0);
                }

                return data;
            }
            catch
            {
                return null;
            }
        }

        private string FindNearbyText(double centerX, double centerY, double radius)
        {
            try
            {
                dynamic ssets = _acadDoc.SelectionSets;
                string ssetName = "TextSearch_" + DateTime.Now.Ticks;
                dynamic sset = ssets.Add(ssetName);

                double[] minPt = new double[] { centerX - radius, centerY - radius, 0 };
                double[] maxPt = new double[] { centerX + radius, centerY + radius, 0 };

                // In AutoCAD COM, Select method uses integers for selection type. 1 = Crossing
                sset.Select(1, minPt, maxPt);

                string foundText = "";
                double minDistance = double.MaxValue;

                for (int i = 0; i < sset.Count; i++)
                {
                    dynamic ent = sset.Item(i);
                    string name = ent.ObjectName;
                    if (name == "AcDbText" || name == "AcDbMText")
                    {
                        var insPt = ent.InsertionPoint;
                        double dx = insPt[0] - centerX;
                        double dy = insPt[1] - centerY;
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            string txt = ent.TextString;
                            if (name == "AcDbMText" && txt.Contains(";"))
                                txt = txt.Substring(txt.LastIndexOf(";") + 1).Replace("}", "");
                            foundText = txt.Trim();
                        }
                    }
                }
                sset.Delete();
                return foundText;
            }
            catch { return ""; }
        }

        private int GetEntityPriority(string entityType)
        {
            if (entityType == "AcDbBlockReference") return 3;
            if (entityType == "AcDbPolyline" || entityType == "AcDb2dPolyline" || entityType == "AcDbCircle") return 2;
            if (entityType == "AcDbHatch") return 1;
            return 0;
        }
    }
}
