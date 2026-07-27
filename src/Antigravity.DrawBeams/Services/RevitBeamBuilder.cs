using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.DrawBeams.Services
{
    public enum BeamCreateStatus
    {
        Created,
        SkippedDuplicate,
        Failed
    }

    public class BeamCreateResult
    {
        public BeamCreateStatus Status { get; set; }
        public ElementId ExistingElementId { get; set; }
        public string Reason { get; set; }
        public FamilyInstance Instance { get; set; }
    }

    public class RevitBeamBuilder
    {
        private readonly Document _doc;

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

        public ElementId FindOverlappingExistingBeam(
            Curve candidateCurve,
            Level level,
            double widthMm,
            double heightMm,
            BeamOverlapOptions options = null)
        {
            if (_doc == null || candidateCurve == null || level == null) return ElementId.InvalidElementId;

            var collector = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType();

            XYZ p1 = candidateCurve.GetEndPoint(0);
            XYZ p2 = candidateCurve.GetEndPoint(1);

            double candStartX = UnitUtils.Convert(p1.X, UnitTypeId.Feet, UnitTypeId.Millimeters);
            double candStartY = UnitUtils.Convert(p1.Y, UnitTypeId.Feet, UnitTypeId.Millimeters);
            double candEndX = UnitUtils.Convert(p2.X, UnitTypeId.Feet, UnitTypeId.Millimeters);
            double candEndY = UnitUtils.Convert(p2.Y, UnitTypeId.Feet, UnitTypeId.Millimeters);

            foreach (FamilyInstance existing in collector.OfType<FamilyInstance>())
            {
                if (existing.LevelId != level.Id) continue;
                if (!(existing.Location is LocationCurve locCurve) || !(locCurve.Curve is Line existLine)) continue;

                XYZ ep1 = existLine.GetEndPoint(0);
                XYZ ep2 = existLine.GetEndPoint(1);

                double existStartX = UnitUtils.Convert(ep1.X, UnitTypeId.Feet, UnitTypeId.Millimeters);
                double existStartY = UnitUtils.Convert(ep1.Y, UnitTypeId.Feet, UnitTypeId.Millimeters);
                double existEndX = UnitUtils.Convert(ep2.X, UnitTypeId.Feet, UnitTypeId.Millimeters);
                double existEndY = UnitUtils.Convert(ep2.Y, UnitTypeId.Feet, UnitTypeId.Millimeters);

                double existWidth = 0, existHeight = 0;
                var symbol = existing.Symbol;
                if (symbol != null)
                {
                    Parameter pB = symbol.LookupParameter("b") ?? symbol.LookupParameter("Width") ?? symbol.LookupParameter("B");
                    Parameter pH = symbol.LookupParameter("h") ?? symbol.LookupParameter("Height") ?? symbol.LookupParameter("H");
                    if (pB != null && pB.HasValue) existWidth = UnitUtils.Convert(pB.AsDouble(), UnitTypeId.Feet, UnitTypeId.Millimeters);
                    if (pH != null && pH.HasValue) existHeight = UnitUtils.Convert(pH.AsDouble(), UnitTypeId.Feet, UnitTypeId.Millimeters);
                }

                if (RevitBeamGuardHelper.IsDuplicateRevitBeam(
                    candStartX, candStartY, candEndX, candEndY, widthMm, heightMm,
                    existStartX, existStartY, existEndX, existEndY, existWidth, existHeight,
                    options))
                {
                    return existing.Id;
                }
            }

            return ElementId.InvalidElementId;
        }

        public BeamCreateResult CreateBeamWithDuplicateGuard(
            Curve curve, FamilySymbol symbol, Level level, double offsetMm, int justification,
            double widthMm, double heightMm, string mark = null, BeamOverlapOptions options = null)
        {
            var revSummary = BeamDiagnosticCollector.Instance.CurrentSession?.RevitSummary;

            BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
            {
                Stage = BeamDiagnosticStage.RevitGuardEvaluation,
                Action = BeamDiagnosticAction.Evaluated,
                Reason = "Evaluating existing Revit model beams for duplicate guard.",
                Width = widthMm,
                Height = heightMm,
                Mark = mark
            });

            ElementId existingId = FindOverlappingExistingBeam(curve, level, widthMm, heightMm, options);
            if (existingId != ElementId.InvalidElementId)
            {
                if (revSummary != null)
                {
                    revSummary.ExistingDuplicatesCount++;
                    revSummary.SkippedCount++;
                }

                BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
                {
                    Stage = BeamDiagnosticStage.RevitGuardDecision,
                    Action = BeamDiagnosticAction.SkippedDuplicate,
                    ExistingRevitElementId = existingId.ToString(),
                    Reason = $"Near-duplicate beam exists in Revit model (ElementId: {existingId}).",
                    Width = widthMm,
                    Height = heightMm,
                    Mark = mark
                });

                return new BeamCreateResult
                {
                    Status = BeamCreateStatus.SkippedDuplicate,
                    ExistingElementId = existingId,
                    Reason = $"Near-duplicate beam exists in Revit model (ElementId: {existingId})."
                };
            }

            try
            {
                FamilyInstance instance = CreateBeam(curve, symbol, level, offsetMm, justification, mark);

                if (revSummary != null)
                {
                    revSummary.CreatedCount++;
                }

                BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
                {
                    Stage = BeamDiagnosticStage.RevitCreateResult,
                    Action = BeamDiagnosticAction.Created,
                    ExistingRevitElementId = instance?.Id.ToString(),
                    Reason = "Successfully created new Revit beam.",
                    Width = widthMm,
                    Height = heightMm,
                    Mark = mark
                });

                return new BeamCreateResult
                {
                    Status = BeamCreateStatus.Created,
                    Instance = instance,
                    Reason = "Successfully created new Revit beam."
                };
            }
            catch (Exception ex)
            {
                if (revSummary != null)
                {
                    revSummary.FailedCount++;
                }

                BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
                {
                    Stage = BeamDiagnosticStage.RevitCreateResult,
                    Action = BeamDiagnosticAction.Failed,
                    ExceptionType = ex.GetType().FullName,
                    ExceptionMessage = ex.Message,
                    Reason = $"Revit beam creation failed: {ex.Message}",
                    Width = widthMm,
                    Height = heightMm,
                    Mark = mark
                });

                return new BeamCreateResult
                {
                    Status = BeamCreateStatus.Failed,
                    Reason = ex.Message
                };
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
