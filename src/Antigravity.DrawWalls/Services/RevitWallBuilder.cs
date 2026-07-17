using Autodesk.Revit.DB;
using Antigravity.DrawWalls.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.DrawWalls.Services
{
    public class RevitWallBuilder
    {
        private Document _doc;
        private Level _baseLevel;
        private Level _topLevel;
        private double _baseOffset;
        private double _topOffset;
        private WallType _templateType;

        public RevitWallBuilder(Document doc, Level baseLevel, Level topLevel, double baseOffset, double topOffset, WallType templateType)
        {
            _doc = doc;
            _baseLevel = baseLevel;
            _topLevel = topLevel;
            _baseOffset = baseOffset / 304.8; // Convert mm to fractional feet
            _topOffset = topOffset / 304.8;
            _templateType = templateType;
        }

        public Wall BuildWall(WallData data)
        {
            try
            {
                // 1. Transform coordinates
                XYZ start = Antigravity.Core.Services.CoordinateService.CadToRevit(data.StartX, data.StartY, 0);
                XYZ end = Antigravity.Core.Services.CoordinateService.CadToRevit(data.EndX, data.EndY, 0);
                
                if (start.DistanceTo(end) < 0.001) return null; // Too short

                Line geomLine = Line.CreateBound(start, end);

                // 2. Determine Wall Type by thickness (mm)
                WallType typeToUse = GetOrCreateWallType(data.Thickness);

                // 3. Create Wall
                Wall wall = Wall.Create(_doc, geomLine, typeToUse.Id, _baseLevel.Id, 10.0, _baseOffset, false, false);
                
                // 4. Set Top Level and Offsets
                Parameter topLevelParam = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
                topLevelParam.Set(_topLevel.Id);
                
                Parameter topOffsetParam = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET);
                topOffsetParam.Set(_topOffset);

                return wall;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Create wall failed: {ex.Message}");
                return null;
            }
        }

        private WallType GetOrCreateWallType(double thicknessMm)
        {
            // Thickness in feet for Revit internal search
            double thicknessFt = thicknessMm / 304.8;
            
            // 1. Check if the user-selected template type already has the correct width
            if (Math.Abs(_templateType.Width - thicknessFt) < 0.001)
            {
                return _templateType;
            }

            // 2. Look for an existing wall type WITH THE SAME NAME as the expected new name
            string newName = _templateType.Name + "_" + Math.Round(thicknessMm, 0) + "mm";
            WallType existingByName = new FilteredElementCollector(_doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(t => t.Name == newName);
            
            if (existingByName != null) return existingByName;

            // 3. Fallback: Duplicate the user-selected type and set its width
            WallType newType = _templateType.Duplicate(newName) as WallType;
            CompoundStructure cs = newType.GetCompoundStructure();
            int layerIndex = cs.GetFirstCoreLayerIndex();
            cs.SetLayerWidth(layerIndex, thicknessFt);
            newType.SetCompoundStructure(cs);
            
            return newType;
        }
    }
}
