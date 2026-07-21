using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.Core.Services
{
    public class RevitFoundationPlacementAdapter : IFoundationPlacementAdapter
    {
        private readonly Document _doc;
        private readonly FamilySymbol _baseType;
        private readonly List<Level> _levels;

        public RevitFoundationPlacementAdapter(Document doc, FamilySymbol baseType, List<Level> levels)
        {
            _doc = doc;
            _baseType = baseType;
            _levels = levels;
        }

        public bool TryResolveLevel(double elevation, out double levelElevation)
        {
            levelElevation = 0;
            var level = ResolveLevel(elevation);
            if (level == null) return false;
            levelElevation = level.Elevation;
            return !double.IsNaN(levelElevation) && !double.IsInfinity(levelElevation);
        }

        public bool TryGetOrCreateFoundationType(double length, double width, out string typeName)
        {
            typeName = string.Empty;
            var existingType = _baseType.Family.GetFamilySymbolIds()
                .Select(id => _doc.GetElement(id) as FamilySymbol)
                .FirstOrDefault(x => 
                {
                    var lenParam = x.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH) ?? x.LookupParameter("Length");
                    var widParam = x.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH) ?? x.LookupParameter("Width");
                    if (lenParam == null || widParam == null || lenParam.StorageType != StorageType.Double || widParam.StorageType != StorageType.Double) return false;
                    return Math.Abs(lenParam.AsDouble() - length) < 0.001 &&
                           Math.Abs(widParam.AsDouble() - width) < 0.001;
                });
            
            if (existingType != null)
            {
                typeName = existingType.Name;
                return true;
            }
            
            var newName = $"Foundation_{Math.Round(length * 304.8)}_{Math.Round(width * 304.8)}";
            while (_baseType.Family.GetFamilySymbolIds()
                .Select(id => _doc.GetElement(id) as FamilySymbol)
                .Any(x => x.Name == newName))
            {
                newName = $"{newName}_{Guid.NewGuid().ToString().Substring(0, 4)}";
            }

            using (var subTx = new SubTransaction(_doc))
            {
                try
                {
                    subTx.Start();
                    var targetType = _baseType.Duplicate(newName) as FamilySymbol;
                    var lParam = targetType.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH) ?? targetType.LookupParameter("Length");
                    var wParam = targetType.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH) ?? targetType.LookupParameter("Width");
                    
                    if (lParam == null || wParam == null || 
                        lParam.IsReadOnly || wParam.IsReadOnly || 
                        lParam.StorageType != StorageType.Double || wParam.StorageType != StorageType.Double) 
                    {
                        subTx.RollBack();
                        return false;
                    }
                    
                    bool lenSuccess = lParam.Set(length);
                    bool widSuccess = wParam.Set(width);
                    
                    if (!lenSuccess || !widSuccess || 
                        Math.Abs(lParam.AsDouble() - length) > 0.001 || 
                        Math.Abs(wParam.AsDouble() - width) > 0.001) 
                    {
                        subTx.RollBack();
                        return false;
                    }
                    
                    if (subTx.Commit() != TransactionStatus.Committed)
                    {
                        return false;
                    }

                    typeName = newName;
                    return true;
                }
                catch
                {
                    if (subTx.HasStarted())
                    {
                        subTx.RollBack();
                    }
                    return false;
                }
            }
        }

        public bool PlaceFoundation(double x, double y, double z, string typeName, double rotationAngle)
        {
            try
            {
                var targetType = _baseType.Family.GetFamilySymbolIds()
                    .Select(id => _doc.GetElement(id) as FamilySymbol)
                    .FirstOrDefault(t => t.Name == typeName);

                if (targetType == null) return false;

                var level = ResolveLevel(z);
                if (level == null) return false;

                using (var subTx = new SubTransaction(_doc))
                {
                    subTx.Start();
                    
                    if (!targetType.IsActive)
                        targetType.Activate();
                        
                    var centerPoint = new XYZ(x, y, z);
                    var instance = _doc.Create.NewFamilyInstance(centerPoint, targetType, level, Autodesk.Revit.DB.Structure.StructuralType.Footing);
                    
                    if (instance == null) 
                    {
                        subTx.RollBack();
                        return false;
                    }

                    if (rotationAngle != 0)
                    {
                        var axis = Line.CreateBound(centerPoint, centerPoint + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(_doc, instance.Id, axis, rotationAngle);
                    }
                    
                    if (subTx.Commit() != TransactionStatus.Committed)
                    {
                        return false;
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private Level ResolveLevel(double elevation)
        {
            if (double.IsNaN(elevation) || double.IsInfinity(elevation) || _levels == null)
                return null;

            var validLevels = _levels
                .Where(level => level != null && (Math.Abs(level.Elevation - elevation) < 0.001 || level.Elevation <= elevation))
                .OrderByDescending(level => level.Elevation)
                .ToList();
            return validLevels.FirstOrDefault() ?? _levels
                .Where(level => level != null)
                .OrderBy(level => level.Elevation)
                .FirstOrDefault();
        }
    }
}
