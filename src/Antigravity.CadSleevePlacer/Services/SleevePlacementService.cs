using Autodesk.Revit.DB;
using Antigravity.CadSleevePlacer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.CadSleevePlacer.Services
{
    public class SleevePlacementService
    {
        /// <summary>
        /// Places Unhosted Generic Model Family Instances or DirectShapes at the extracted CAD locations.
        /// </summary>
        public static int PlaceSleeves(
            Document doc,
            List<CadSleeveInfo> sleeves,
            GridMappingService mapper,
            FamilySymbol sleeveSymbol,
            ElementId levelId,
            bool isDirectShape,
            double defaultLengthMm,
            out List<string> errorLogs)
        {
            errorLogs = new List<string>();
            if (doc == null || sleeves == null || mapper == null || (!isDirectShape && sleeveSymbol == null)) return 0;

            Level level = null;
            double levelElevFt = 0.0;
            if (levelId != null && levelId != ElementId.InvalidElementId)
            {
                level = doc.GetElement(levelId) as Level;
                if (level != null) levelElevFt = level.Elevation;
            }

            int placed = 0;

            using (var trans = new Transaction(doc, "Place CAD Sleeves"))
            {
                trans.Start();

                if (!isDirectShape && sleeveSymbol != null)
                {
                    if (!sleeveSymbol.IsActive)
                        sleeveSymbol.Activate();
                }

                foreach (var sleeve in sleeves)
                {
                    try
                    {
                        // Absolute center elevation in feet (used as insertion-point Z when no Level is set)
                        double zOffsetFt = CalcCenterElevFt(
                            sleeve.ElevationMm, sleeve.ElevationType,
                            sleeve.IsRectangular ? sleeve.HeightMm : sleeve.DiameterMm,
                            levelElevFt);
                        XYZ revitPt = mapper.CadToRevit(sleeve.TargetPoint[0], sleeve.TargetPoint[1], 0); // Z = 0

                         if (isDirectShape)
                        {
                            // Create DirectShape
                            double sleeveHeightMm = defaultLengthMm > 0 ? defaultLengthMm : 300.0;
                            double sleeveHeightFt = GridMappingService.MmToFeet(sleeveHeightMm);

                            // Calculate center Z elevation in Revit feet.
                            // ElevationMm is the raw annotation value (relative to level).
                            // When ElevationType is BOO/BOD/BOP, the value is the BOTTOM of the opening,
                            // so we add half the section height to convert to center.
                            double zCopFt = CalcCenterElevFt(
                                sleeve.ElevationMm, sleeve.ElevationType,
                                sleeve.IsRectangular ? sleeve.HeightMm : sleeve.DiameterMm,
                                levelElevFt);

                            // Center horizontally along Y axis
                            double baseYFt = revitPt.Y - (sleeveHeightFt / 2.0);

                            XYZ geometryCenter = new XYZ(revitPt.X, baseYFt, zCopFt);


                            Solid solid = null;
                            if (sleeve.IsRectangular)
                            {
                                double wFt = GridMappingService.MmToFeet(sleeve.WidthMm);
                                double hFt = GridMappingService.MmToFeet(sleeve.HeightMm);
                                solid = CreateBoxSolid(geometryCenter, wFt, hFt, sleeveHeightFt);
                            }
                            else
                            {
                                double radiusFt = GridMappingService.MmToFeet(sleeve.DiameterMm) / 2.0;
                                solid = CreateCylinderSolid(geometryCenter, radiusFt, sleeveHeightFt);
                            }

                            if (solid != null)
                            {
                                DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                                if (ds != null)
                                {
                                    ds.SetShape(new GeometryObject[] { solid });
                                    ds.Name = string.IsNullOrEmpty(sleeve.SleeveType) ? "CAD Sleeve Void" : sleeve.SleeveType;

                                    if (level != null)
                                    {
                                        Parameter levelParam = ds.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                                        if (levelParam != null && !levelParam.IsReadOnly)
                                        {
                                            levelParam.Set(level.Id);
                                        }
                                    }

                                    Parameter commentsParam = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                                    if (commentsParam != null && !commentsParam.IsReadOnly)
                                    {
                                        string details = sleeve.IsRectangular 
                                            ? $"{sleeve.WidthMm}x{sleeve.HeightMm}" 
                                            : $"DN{sleeve.DiameterMm}";
                                        commentsParam.Set($"Placed by CAD: {sleeve.SleeveType} ({details})");
                                    }

                                    placed++;

                                    // Color element red in active view
                                    ColorElementInActiveView(doc, ds.Id, new Color(255, 0, 0));

                                    // Rotate DirectShape to align with CAD block orientation.
                                    // RotationRad = angle of the LONG side (700mm width) of the polyline block,
                                    // and the box is built with width (700) along X-axis.
                                    // Rotating by RotationRad directly aligns the 700-side with the wall face
                                    // (same convention as FamilyInstance path below).
                                    // Note: the previous offset of -π/2 was WRONG because it un-did the alignment
                                    // when the block's long side was vertical (rotRad=π/2 → π/2-π/2=0 → no rotation).
                                    XYZ solidCenter = new XYZ(revitPt.X, revitPt.Y, zCopFt);
                                    RotateElement(doc, ds, solidCenter, sleeve.RotationRad);
                                }
                            }
                            else
                            {
                                throw new Exception("Không thể tạo Solid từ các thông số hình học (độ dài, chiều rộng, chiều cao không hợp lệ).");
                            }
                        }
                        else
                        {
                            // Place Family Instance (Unhosted)
                            FamilyInstance fi;
                            if (level != null)
                            {
                                fi = doc.Create.NewFamilyInstance(revitPt, sleeveSymbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }
                            else
                            {
                                revitPt = mapper.CadToRevit(sleeve.TargetPoint[0], sleeve.TargetPoint[1], zOffsetFt);
                                fi = doc.Create.NewFamilyInstance(revitPt, sleeveSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }

                            if (fi != null)
                            {
                                // Assign Offset: for FamilyInstance the offset is from level to CENTER of sleeve
                                // CalcCenterElevFt already returns absolute Z (levelElev + offset);
                                // the family instance offset param is relative to level, so subtract levelElevFt.
                                double fiCenterAbsFt = CalcCenterElevFt(
                                    sleeve.ElevationMm, sleeve.ElevationType,
                                    sleeve.IsRectangular ? sleeve.HeightMm : sleeve.DiameterMm,
                                    0.0); // pass 0 so result is offset-from-level only
                                Parameter offsetParam = fi.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                                if (offsetParam == null) offsetParam = fi.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                                if (offsetParam == null) offsetParam = fi.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM);
                                
                                if (offsetParam != null && !offsetParam.IsReadOnly)
                                {
                                    offsetParam.Set(fiCenterAbsFt);
                                }

                                // Set Dimension Parameters
                                if (sleeve.IsRectangular)
                                {
                                    double wFt = GridMappingService.MmToFeet(sleeve.WidthMm);
                                    double hFt = GridMappingService.MmToFeet(sleeve.HeightMm);
                                    SetParameter(fi, "Width", wFt);
                                    SetParameter(fi, "Height", hFt);
                                    SetParameter(fi, "W", wFt);
                                    SetParameter(fi, "H", hFt);
                                }
                                else
                                {
                                    double diameterFt = GridMappingService.MmToFeet(sleeve.DiameterMm);
                                    SetParameter(fi, "Diameter", diameterFt);
                                    SetParameter(fi, "DN", diameterFt);
                                    SetParameter(fi, "Radius", diameterFt / 2.0);
                                }

                                // Try to set length parameter for the Family Instance
                                double lengthFt = GridMappingService.MmToFeet(defaultLengthMm > 0 ? defaultLengthMm : 300.0);
                                SetParameter(fi, "Length", lengthFt);
                                SetParameter(fi, "L", lengthFt);
                                SetParameter(fi, "Thickness", lengthFt);
                                SetParameter(fi, "Depth", lengthFt);
                                SetParameter(fi, "T", lengthFt);

                                Parameter commentsParam = fi.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                                if (commentsParam != null && !commentsParam.IsReadOnly)
                                {
                                    commentsParam.Set($"Placed by CAD: {sleeve.SleeveType}");
                                }

                                placed++;

                                // Color element red in active view
                                ColorElementInActiveView(doc, fi.Id, new Color(255, 0, 0));

                                // Rotate FamilyInstance to match slanted CAD beam/wall orientation
                                XYZ fiPt = level != null ? revitPt : new XYZ(revitPt.X, revitPt.Y, zOffsetFt);
                                // Since standard unhosted Revit families are modeled with their length along the horizontal X-axis (0 rad),
                                // we rotate them by sleeve.RotationRad directly to align them perfectly with the CAD angle.
                                RotateElement(doc, fi, fiPt, sleeve.RotationRad);
                            }
                            else
                            {
                                throw new Exception("Không thể tạo đối tượng NewFamilyInstance (vui lòng kiểm tra lại loại Family xem có cần Host như Tường/Sàn không).");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        string details = sleeve.IsRectangular 
                            ? $"{sleeve.WidthMm}x{sleeve.HeightMm}" 
                            : $"DN{sleeve.DiameterMm}";
                        errorLogs.Add($"Sleeve {details} tại ({sleeve.TargetPoint[0]:F0}, {sleeve.TargetPoint[1]:F0}): {ex.Message}");
                    }
                }

                trans.Commit();
            }

            return placed;
        }

        /// <summary>
        /// Converts a raw annotation elevation to the center elevation in Revit feet.
        /// 
        /// When ElevationType is BOO/BOD/BOP (bottom-of-opening/duct/pipe), the annotation
        /// records the BOTTOM of the cross-section. We add half the section size to get the center.
        /// When ElevationType is COP/TOP or unknown, the value is already the center (or top).
        /// For TOP, we subtract half the size.
        /// 
        /// The result is in Revit feet and is ABSOLUTE (includes levelElevFt).
        /// </summary>
        private static double CalcCenterElevFt(double elevMm, string elevType, double sizeMm, double levelElevFt)
        {
            double rawFt   = GridMappingService.MmToFeet(elevMm);
            double halfFt  = GridMappingService.MmToFeet(sizeMm) / 2.0;

            switch ((elevType ?? "").ToUpperInvariant())
            {
                case "BOO":
                case "BOD":
                case "BOP":
                    // Bottom elevation → center is bottom + half-height
                    return levelElevFt + rawFt + halfFt;

                case "TOP":
                    // Top elevation → center is top - half-height
                    return levelElevFt + rawFt - halfFt;

                default:
                    // COP, unknown, or empty → treat as center elevation
                    return levelElevFt + rawFt;
            }
        }

        private static Solid CreateCylinderSolid(XYZ center, double radiusFeet, double heightFeet)
        {
            try
            {
                // Profile in XZ plane (BasisX and BasisZ)
                Arc arc1 = Arc.Create(center, radiusFeet, 0.0, Math.PI, XYZ.BasisX, XYZ.BasisZ);
                Arc arc2 = Arc.Create(center, radiusFeet, Math.PI, 2.0 * Math.PI, XYZ.BasisX, XYZ.BasisZ);
                
                CurveLoop loop = new CurveLoop();
                loop.Append(arc1);
                loop.Append(arc2);
                
                var loops = new List<CurveLoop> { loop };
                // Extrude horizontally along Y-axis (BasisY)
                return GeometryCreationUtilities.CreateExtrusionGeometry(loops, XYZ.BasisY, heightFeet);
            }
            catch
            {
                return null;
            }
        }

        private static Solid CreateBoxSolid(XYZ center, double wFeet, double hFeet, double heightFeet)
        {
            try
            {
                // Profile in XZ plane centered at center
                // wFeet is width along X, hFeet is height along Z (vertical)
                XYZ p0 = new XYZ(center.X - wFeet / 2.0, center.Y, center.Z - hFeet / 2.0);
                XYZ p1 = new XYZ(center.X + wFeet / 2.0, center.Y, center.Z - hFeet / 2.0);
                XYZ p2 = new XYZ(center.X + wFeet / 2.0, center.Y, center.Z + hFeet / 2.0);
                XYZ p3 = new XYZ(center.X - wFeet / 2.0, center.Y, center.Z + hFeet / 2.0);

                Line line1 = Line.CreateBound(p0, p1);
                Line line2 = Line.CreateBound(p1, p2);
                Line line3 = Line.CreateBound(p2, p3);
                Line line4 = Line.CreateBound(p3, p0);

                CurveLoop loop = new CurveLoop();
                loop.Append(line1);
                loop.Append(line2);
                loop.Append(line3);
                loop.Append(line4);

                var loops = new List<CurveLoop> { loop };
                // Extrude horizontally along Y-axis (BasisY)
                return GeometryCreationUtilities.CreateExtrusionGeometry(loops, XYZ.BasisY, heightFeet);
            }
            catch
            {
                return null;
            }
        }

        private static void SetParameter(FamilyInstance fi, string paramName, double value)
        {
            Parameter param = fi.LookupParameter(paramName);
            if (param != null && !param.IsReadOnly)
            {
                param.Set(value);
            }
        }

        private static void RotateElement(Document doc, Element elem, XYZ center, double angleRad)
        {
            if (Math.Abs(angleRad) < 0.001) return;
            try
            {
                // Rotate element around vertical Z axis passing through its center
                Line axis = Line.CreateBound(center, center + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc, elem.Id, axis, angleRad);
            }
            catch
            {
                // Skip if rotation fails
            }
        }

        private static void ColorElementInActiveView(Document doc, ElementId elemId, Color color)
        {
            try
            {
                View activeView = doc.ActiveView;
                if (activeView != null)
                {
                    // Find a solid fill pattern in the document
                    FillPatternElement solidFill = new FilteredElementCollector(doc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);

                    if (solidFill != null)
                    {
                        OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                        
                        // Set projection line color
                        ogs.SetProjectionLineColor(color);
                        
                        // Set surface foreground pattern color and solid pattern
                        ogs.SetSurfaceForegroundPatternColor(color);
                        ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                        
                        // Set cut foreground pattern color and solid pattern
                        ogs.SetCutForegroundPatternColor(color);
                        ogs.SetCutForegroundPatternId(solidFill.Id);

                        activeView.SetElementOverrides(elemId, ogs);
                    }
                }
            }
            catch
            {
                // Skip if view override fails
            }
        }
    }
}
