using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.DrawWalls.Models;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Antigravity.DrawWalls.Services
{
    public class WallCreationHandler : IExternalEventHandler
    {
        public List<WallData> WallDatas { get; set; }
        public Level BaseLevel { get; set; }
        public Level TopLevel { get; set; }
        public double BaseOffset { get; set; }
        public double TopOffset { get; set; }
        public WallType TemplateType { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;

            if (WallDatas == null || WallDatas.Count == 0) return;

            try
            {
                RevitWallBuilder builder = new RevitWallBuilder(doc, BaseLevel, TopLevel, BaseOffset, TopOffset, TemplateType);

                using (Transaction t = new Transaction(doc, "Dựng vách từ CAD"))
                {
                    t.Start();
                    int successCount = 0;
                    foreach (var data in WallDatas)
                    {
                        Wall wall = builder.BuildWall(data);
                        if (wall != null)
                        {
                            successCount++;
                        }
                    }
                    t.Commit();
                    MessageBox.Show($"Đã tạo thành công {successCount} vách trong Revit.", "Kết quả");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi xử lý trong Revit: " + ex.Message);
            }
        }

        public string GetName()
        {
            return "WallCreationHandler";
        }
    }
}
