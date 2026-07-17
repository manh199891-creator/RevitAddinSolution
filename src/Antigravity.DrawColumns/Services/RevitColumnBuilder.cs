using Autodesk.Revit.DB;
using Antigravity.DrawColumns.Models;
using System;
using System.Collections.Generic;

namespace Antigravity.DrawColumns.Services
{
    public class RevitColumnBuilder
    {
        private Document _doc;
        private FamilySymbol _baseSymbolRect;
        private FamilySymbol _baseSymbolCirc;
        private Level _baseLevel;
        private Level _topLevel;
        private double _botOffset;
        private double _topOffset;
        private string _paramB;
        private string _paramH;
        private string _paramDia;

        public RevitColumnBuilder(Document doc, 
            FamilySymbol baseSymbolRect, FamilySymbol baseSymbolCirc,
            Level baseLevel, Level topLevel, 
            double botOffset, double topOffset,
            string paramB, string paramH, string paramDia)
        {
            _doc = doc;
            _baseSymbolRect = baseSymbolRect;
            _baseSymbolCirc = baseSymbolCirc;
            _baseLevel = baseLevel;
            _topLevel = topLevel;
            _botOffset = botOffset;
            _topOffset = topOffset;
            _paramB = paramB;
            _paramH = paramH;
            _paramDia = paramDia;
        }

        public int BuildColumns(List<RevitColumnData> cadColumns)
        {
            int count = 0;
            using (Transaction t = new Transaction(_doc, "Tạo Cột từ CAD"))
            {
                t.Start();
                
                if (_baseSymbolRect != null && !_baseSymbolRect.IsActive) _baseSymbolRect.Activate();
                if (_baseSymbolCirc != null && !_baseSymbolCirc.IsActive) _baseSymbolCirc.Activate();

                foreach (var col in cadColumns)
                {
                    try
                    {
                        XYZ location = Antigravity.Core.Services.CoordinateService.CadToRevit(col.X, col.Y, _baseLevel.ProjectElevation);

                        double width_ft = col.Width / 304.8;
                        double height_ft = col.Height / 304.8;
                        double radius_ft = col.Radius / 304.8;
                        
                        FamilySymbol columnType = GetOrCreateColumnType(col, width_ft, height_ft, radius_ft);
                        if (columnType == null) continue;
                        
                        if (!columnType.IsActive)
                            columnType.Activate();

                        FamilyInstance instance = _doc.Create.NewFamilyInstance(location, columnType, _baseLevel, Autodesk.Revit.DB.Structure.StructuralType.Column);

                        // Set Offsets
                        Parameter pBaseOffset = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                        if (pBaseOffset != null && !pBaseOffset.IsReadOnly) pBaseOffset.Set(_botOffset / 304.8);

                        if (_topLevel != null)
                        {
                            Parameter pTopLevel = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            if (pTopLevel != null && !pTopLevel.IsReadOnly) pTopLevel.Set(_topLevel.Id);
                            
                            Parameter pTopOffset = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                            if (pTopOffset != null && !pTopOffset.IsReadOnly) pTopOffset.Set(_topOffset / 304.8);
                        }

                        if (col.Rotation != 0)
                        {
                            Line axis = Line.CreateBound(location, location + XYZ.BasisZ);
                            ElementTransformUtils.RotateElement(_doc, instance.Id, axis, col.Rotation);
                        }

                        if (!string.IsNullOrEmpty(col.ColumnName))
                        {
                            Parameter pMark = instance.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                            if (pMark != null && !pMark.IsReadOnly) pMark.Set(col.ColumnName);
                        }
                        
                        count++;
                    }
                    catch (Exception)
                    {
                        // Skip if failed
                    }
                }
                
                t.Commit();
            }
            return count;
        }

        private FamilySymbol GetOrCreateColumnType(RevitColumnData col, double widthFt, double heightFt, double radiusFt)
        {
            if (col.IsRound)
            {
                if (_baseSymbolCirc == null) return null;

                int d_mm = (int)Math.Round((radiusFt * 2) * 304.8);
                string typeName = string.Format("D{0}", d_mm);

                Family family = _baseSymbolCirc.Family;
                ISet<ElementId> symbolIds = family.GetFamilySymbolIds();
                foreach (ElementId id in symbolIds)
                {
                    FamilySymbol sym = _doc.GetElement(id) as FamilySymbol;
                    if (sym != null && sym.Name == typeName) return sym;
                }

                FamilySymbol newSymbol = _baseSymbolCirc.Duplicate(typeName) as FamilySymbol;
                Parameter pDia = newSymbol.LookupParameter(_paramDia);
                if (pDia != null && !pDia.IsReadOnly) pDia.Set(radiusFt * 2);

                return newSymbol;
            }
            else
            {
                if (_baseSymbolRect == null) return null;

                int w_mm = (int)Math.Round(widthFt * 304.8);
                int h_mm = (int)Math.Round(heightFt * 304.8);
                string typeName = string.Format("{0}x{1}", w_mm, h_mm);

                Family family = _baseSymbolRect.Family;
                ISet<ElementId> symbolIds = family.GetFamilySymbolIds();
                foreach (ElementId id in symbolIds)
                {
                    FamilySymbol sym = _doc.GetElement(id) as FamilySymbol;
                    if (sym != null && sym.Name == typeName) return sym;
                }

                FamilySymbol newSymbol = _baseSymbolRect.Duplicate(typeName) as FamilySymbol;
                
                Parameter pB = newSymbol.LookupParameter(_paramB);
                Parameter pH = newSymbol.LookupParameter(_paramH);
                
                if (pB != null && !pB.IsReadOnly) pB.Set(widthFt);
                if (pH != null && !pH.IsReadOnly) pH.Set(heightFt);

                return newSymbol;
            }
        }
    }
}
