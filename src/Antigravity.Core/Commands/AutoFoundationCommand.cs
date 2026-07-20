using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Antigravity.Core.Services;

namespace Antigravity.Core.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoFoundationCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var doc = uiapp.ActiveUIDocument.Document;
            var uidoc = uiapp.ActiveUIDocument;

            try
            {
                var reference = uidoc.Selection.PickObject(ObjectType.Element, "Select CAD Import/Link");
                var importInstance = doc.GetElement(reference) as ImportInstance;
                
                if (importInstance == null)
                {
                    message = "Selected element is not a CAD ImportInstance.";
                    return Result.Failed;
                }

                var parser = new CadParserService();
                var foundationsData = parser.ExtractFoundationData(importInstance, "S-FND");

                if (foundationsData.Count == 0)
                {
                    message = "No valid foundations found in the CAD layer 'S-FND'.";
                    return Result.Failed;
                }

                using (var tx = new Transaction(doc, "Auto Place Foundations"))
                {
                    tx.Start();

                    var foundationType = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(x => 
                            x.Family.FamilyPlacementType == FamilyPlacementType.OneLevelBased &&
                            x.LookupParameter("Length") != null && !x.LookupParameter("Length").IsReadOnly &&
                            x.LookupParameter("Width") != null && !x.LookupParameter("Width").IsReadOnly);

                    if (foundationType == null)
                    {
                        message = "No valid Structural Foundation Family with OneLevelBased placement and writable 'Length' and 'Width' parameters found.";
                        return Result.Failed;
                    }

                    if (!foundationType.IsActive)
                        foundationType.Activate();

                    var level = doc.ActiveView.GenLevel;
                    if (level == null)
                    {
                        level = new FilteredElementCollector(doc)
                            .OfClass(typeof(Level))
                            .FirstElement() as Level;
                    }

                    foreach (var data in foundationsData)
                    {
                        var targetType = foundationType;
                        
                        if (data.Length > 0 && data.Width > 0)
                        {
                            var existingType = foundationType.Family.GetFamilySymbolIds()
                                .Select(id => doc.GetElement(id) as FamilySymbol)
                                .FirstOrDefault(x => 
                                {
                                    var lenParam = x.LookupParameter("Length");
                                    var widParam = x.LookupParameter("Width");
                                    if (lenParam == null || widParam == null) return false;
                                    return Math.Abs(lenParam.AsDouble() - data.Length) < 0.001 &&
                                           Math.Abs(widParam.AsDouble() - data.Width) < 0.001;
                                });
                            
                            if (existingType != null)
                            {
                                targetType = existingType;
                            }
                            else
                            {
                                var typeName = $"Foundation_{Math.Round(data.Length * 304.8)}_{Math.Round(data.Width * 304.8)}";
                                
                                while (foundationType.Family.GetFamilySymbolIds()
                                    .Select(id => doc.GetElement(id) as FamilySymbol)
                                    .Any(x => x.Name == typeName))
                                {
                                    typeName = $"{typeName}_{Guid.NewGuid().ToString().Substring(0, 4)}";
                                }

                                targetType = foundationType.Duplicate(typeName) as FamilySymbol;
                                targetType.LookupParameter("Length").Set(data.Length);
                                targetType.LookupParameter("Width").Set(data.Width);
                            }
                        }

                        if (!targetType.IsActive)
                            targetType.Activate();

                        var instance = doc.Create.NewFamilyInstance(data.Center, targetType, level, Autodesk.Revit.DB.Structure.StructuralType.Footing);
                        
                        if (data.RotationAngle != 0)
                        {
                            var axis = Line.CreateBound(data.Center, data.Center + XYZ.BasisZ);
                            ElementTransformUtils.RotateElement(doc, instance.Id, axis, data.RotationAngle);
                        }
                    }

                    if (tx.Commit() != TransactionStatus.Committed)
                    {
                        message = "Transaction failed to commit.";
                        return Result.Failed;
                    }
                }

                TaskDialog.Show("Success", $"Created {foundationsData.Count} foundations.");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
