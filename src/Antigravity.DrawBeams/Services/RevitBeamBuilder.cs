using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.DrawBeams.Services
{
    public class RevitBeamBuilder
    {
        private Document _doc;

        public RevitBeamBuilder(Document doc)
        {
            _doc = doc;
        }

        public FamilySymbol GetOrAddBeamType(Family family, double widthMm, double heightMm, string paramB, string paramH)
        {
            string typeName = $"{widthMm}x{heightMm}";
            
            // Look for existing type in symbols
            var symbols = family.GetFamilySymbolIds().Select(id => _doc.GetElement(id) as FamilySymbol);
            var existing = symbols.FirstOrDefault(s => s.Name == typeName);
            if (existing != null) return existing;

            // Duplicate from first symbol
            var firstSymbol = symbols.FirstOrDefault();
            if (firstSymbol == null) return null;

            FamilySymbol newSymbol = firstSymbol.Duplicate(typeName) as FamilySymbol;
            
            // Set parameters B and H
            SetParameter(newSymbol, paramB, widthMm);
            SetParameter(newSymbol, paramH, heightMm);

            return newSymbol;
        }

        private void SetParameter(Element elem, string paramName, double valueMm)
        {
            Parameter p = elem.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly)
            {
                // In Revit 2024, we convert from Millimeters to Feet (Internal)
                double internalValue = UnitUtils.Convert(valueMm, UnitTypeId.Millimeters, UnitTypeId.Feet);
                p.Set(internalValue);
            }
        }

        public FamilyInstance CreateBeam(Curve curve, FamilySymbol symbol, Level level, double offsetMm, int justification, string mark = null)
        {
            if (!symbol.IsActive) symbol.Activate();

            FamilyInstance beam = _doc.Create.NewFamilyInstance(curve, symbol, level, StructuralType.Beam);

            // Ép Revit cập nhật trạng thái dầm trước khi gán Parameter
            _doc.Regenerate();

            // FIX: Force set cả Reference Level và Schedule Level để đảm bảo dầm không bị nhảy sang VIA HE
            Parameter refLevelParam = beam.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
            if (refLevelParam != null && !refLevelParam.IsReadOnly)
            {
                refLevelParam.Set(level.Id);
            }
            
            Parameter scheduleLevelParam = beam.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
            if (scheduleLevelParam != null && !scheduleLevelParam.IsReadOnly)
            {
                scheduleLevelParam.Set(level.Id);
            }
            
            // Set Justification (0=Left, 1=Center, 2=Right)
            Parameter pJust = beam.get_Parameter(BuiltInParameter.Y_JUSTIFICATION);
            if (pJust != null && !pJust.IsReadOnly) pJust.Set(justification);

            // Also set YZ_JUSTIFICATION to Uniform to ensure Y_JUSTIFICATION takes effect
            Parameter pYZ = beam.get_Parameter(BuiltInParameter.YZ_JUSTIFICATION);
            if (pYZ != null && !pYZ.IsReadOnly) pYZ.Set(0); // 0 = Uniform

            if (offsetMm != 0)
            {
                double internalOffset = UnitUtils.Convert(offsetMm, UnitTypeId.Millimeters, UnitTypeId.Feet);
                Parameter pStart = beam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION);
                Parameter pEnd = beam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION);
                pStart?.Set(internalOffset);
                pEnd?.Set(internalOffset);
            }

            if (!string.IsNullOrEmpty(mark))
            {
                Parameter pMark = beam.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                if (pMark != null && !pMark.IsReadOnly)
                {
                    pMark.Set(mark);
                }
            }

            return beam;
        }

        public void DrawDebugLine(XYZ start, XYZ end)
        {
            try
            {
                Line line = Line.CreateBound(start, end);
                Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, start);
                SketchPlane sketchPlane = SketchPlane.Create(_doc, plane);
                ModelCurve modelCurve = _doc.Create.NewModelCurve(line, sketchPlane);
            }
            catch { }
        }
    }
}
