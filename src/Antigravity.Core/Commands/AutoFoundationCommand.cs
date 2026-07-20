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

                var foundationType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                    .Cast<FamilySymbol>()
                    .FirstOrDefault();

                if (foundationType == null)
                {
                    message = "No Structural Foundation Family found.";
                    return Result.Failed;
                }

                var parser = new CadParserService();
                var foundationsData = parser.ExtractFoundationData(importInstance, "S-FND");

                using (var tx = new Transaction(doc, "Auto Place Foundations"))
                {
                    tx.Start();

                    if (!foundationType.IsActive)
                        foundationType.Activate();

                    var level = new FilteredElementCollector(doc)
                        .OfClass(typeof(Level))
                        .FirstElement() as Level;

                    foreach (var data in foundationsData)
                    {
                        var instance = doc.Create.NewFamilyInstance(data.Center, foundationType, level, Autodesk.Revit.DB.Structure.StructuralType.Footing);
                        
                        if (data.RotationAngle != 0)
                        {
                            var axis = Line.CreateBound(data.Center, data.Center + XYZ.BasisZ);
                            ElementTransformUtils.RotateElement(doc, instance.Id, axis, data.RotationAngle);
                        }
                    }

                    tx.Commit();
                }

                TaskDialog.Show("Success", $"Created {foundationsData.Count} foundations.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
