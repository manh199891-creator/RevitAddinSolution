using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.CadSleevePlacer.Models;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Antigravity.CadSleevePlacer.Services
{
    public class SleevePlacementHandler : IExternalEventHandler
    {
        public List<CadSleeveInfo> Sleeves { get; set; }
        public GridMappingService Mapper { get; set; }
        public FamilySymbol SleeveSymbol { get; set; }
        public ElementId LevelId { get; set; }
        public bool IsDirectShape { get; set; }
        public double DefaultLengthMm { get; set; } = 300.0;

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;

            if (Sleeves == null || Sleeves.Count == 0 || Mapper == null) return;

            try
            {
                List<string> errorLogs = null;
                int placed = SleevePlacementService.PlaceSleeves(
                    doc, Sleeves, Mapper, SleeveSymbol, LevelId, IsDirectShape, DefaultLengthMm, out errorLogs);
                
                string debugInfo = "";
                if (placed > 0)
                {
                    var first = Sleeves[0];
                    double zFt = GridMappingService.MmToFeet(first.ElevationMm);
                    XYZ pt = Mapper.CadToRevit(first.TargetPoint[0], first.TargetPoint[1], 0);
                    debugInfo = $"\n\n[Debug] Toạ độ đặt mẫu (Sleeve 1):\nX: {pt.X:F2} ft\nY: {pt.Y:F2} ft\nZ Offset: {zFt:F2} ft";
                }

                string reportMsg = $"Thành công! Đã đặt {placed}/{Sleeves.Count} sleeve.{debugInfo}";
                if (errorLogs != null && errorLogs.Count > 0)
                {
                    reportMsg += $"\n\n--- DANH SÁCH LỖI CHI TIẾT ({errorLogs.Count} sleeve thất bại) ---\n" + 
                                 string.Join("\n", errorLogs);
                    MessageBox.Show(reportMsg, "Sleeve Placement Report (With Errors)", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(reportMsg, "Sleeve Placement Report", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public string GetName()
        {
            return "SleevePlacementHandler";
        }
    }
}
