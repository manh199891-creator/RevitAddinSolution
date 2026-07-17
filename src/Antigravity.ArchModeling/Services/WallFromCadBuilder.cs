using Autodesk.Revit.DB;
using Antigravity.ArchModeling.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.ArchModeling.Services
{
    public class WallFromCadBuilder
    {
        private Document _doc;
        private Level _baseLevel;
        private Level _topLevel;
        private double _baseOffset;
        private double _topOffset;

        public WallFromCadBuilder(Document doc, Level baseLevel, Level topLevel, double baseOffset, double topOffset)
        {
            _doc = doc;
            _baseLevel = baseLevel;
            _topLevel = topLevel;
            _baseOffset = baseOffset / 304.8;
            _topOffset = topOffset / 304.8;
        }

        public Wall BuildWall(WallData data, WallType templateType)
        {
            try
            {
                if (data.CenterLine == null || data.CenterLine.Length < 0.001) return null;
                if (templateType == null) return null;

                WallType typeToUse = GetOrCreateWallType(templateType, data.ThicknessMm);

                Wall wall = Wall.Create(_doc, data.CenterLine, typeToUse.Id, _baseLevel.Id, 10.0, _baseOffset, false, false);
                
                Parameter topLevelParam = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
                topLevelParam?.Set(_topLevel.Id);
                
                Parameter topOffsetParam = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET);
                topOffsetParam?.Set(_topOffset);

                return wall;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Create wall failed: {ex.Message}");
                return null;
            }
        }

        private WallType GetOrCreateWallType(WallType templateType, double thicknessMm)
        {
            double thicknessFt = thicknessMm / 304.8;
            
            if (Math.Abs(templateType.Width - thicknessFt) < 0.01)
            {
                return templateType;
            }

            string newName = templateType.Name + "_" + Math.Round(thicknessMm, 0) + "mm";
            WallType existingByName = new FilteredElementCollector(_doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(t => t.Name == newName);
            
            if (existingByName != null) return existingByName;

            WallType newType = templateType.Duplicate(newName) as WallType;
            CompoundStructure cs = newType.GetCompoundStructure();
            if (cs != null)
            {
                int layerIndex = cs.GetFirstCoreLayerIndex();
                if (layerIndex >= 0)
                {
                    cs.SetLayerWidth(layerIndex, thicknessFt);
                    newType.SetCompoundStructure(cs);
                }
            }
            
            return newType;
        }
    }
}
