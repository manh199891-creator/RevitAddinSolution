using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.DrawColumns.Models;

namespace Antigravity.DrawColumns.Services
{
    public class BuildColumnsEventHandler : IExternalEventHandler
    {
        public List<RevitColumnData> CadColumns { get; set; }
        
        public FamilySymbol BaseSymbolRect { get; set; }
        public FamilySymbol BaseSymbolCirc { get; set; }
        public Level BaseLevel { get; set; }
        public Level TopLevel { get; set; }
        public double BotOffset { get; set; }
        public double TopOffset { get; set; }
        public string ParamB { get; set; }
        public string ParamH { get; set; }
        public string ParamDia { get; set; }

        public void Execute(UIApplication app)
        {
            if (CadColumns == null || CadColumns.Count == 0) return;
            var doc = app.ActiveUIDocument.Document;
            
            try
            {
                RevitColumnBuilder builder = new RevitColumnBuilder(
                    doc,
                    BaseSymbolRect, BaseSymbolCirc,
                    BaseLevel, TopLevel,
                    BotOffset, TopOffset,
                    ParamB, ParamH, ParamDia);

                int successCount = builder.BuildColumns(CadColumns);
                TaskDialog.Show("Hoàn thành", string.Format("Đã tạo thành công {0} cột trong Revit.", successCount));
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi", "Lỗi tạo cột: " + ex.Message);
            }
        }

        public string GetName()
        {
            return "Build Columns Event Handler";
        }
    }
}
