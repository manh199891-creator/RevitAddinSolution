using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Antigravity.DrawColumns.Services
{
    public class AutoColumnPayload
    {
        public bool IsCircle { get; set; }
        public Level LevelBot { get; set; }
        public Level LevelTop { get; set; }
        public double OffsetBot { get; set; }
        public double OffsetTop { get; set; }
        public FamilySymbol FamilyType { get; set; }
        public string ParamB { get; set; }
        public string ParamH { get; set; }
        public string ParamDia { get; set; }
    }

    public class AutoColumnRevitEventHandler : IExternalEventHandler
    {
        public AutoColumnPayload Payload { get; set; }
        private readonly ICadColumnParserService _parser;

        public AutoColumnRevitEventHandler(ICadColumnParserService parser)
        {
            _parser = parser;
        }

        public void Execute(UIApplication app)
        {
            if (Payload == null) return;
            var doc = app.ActiveUIDocument.Document;
            var uidoc = app.ActiveUIDocument;

            try
            {
                Reference cadRef = uidoc.Selection.PickObject(ObjectType.Element, "Select CAD Import Instance");
                var cadInstance = doc.GetElement(cadRef) as ImportInstance;
                if (cadInstance == null) return;

                var columnsData = _parser.ExtractColumns(cadInstance);

                using (Transaction tx = new Transaction(doc, "Auto Column Placement"))
                {
                    tx.Start();

                    foreach (var colData in columnsData)
                    {
                        if (colData.IsCircle != Payload.IsCircle) continue;

                        // Create instance
                        if (!Payload.FamilyType.IsActive)
                        {
                            Payload.FamilyType.Activate();
                            doc.Regenerate();
                        }

                        var instance = doc.Create.NewFamilyInstance(colData.Centroid, Payload.FamilyType, Payload.LevelBot, Autodesk.Revit.DB.Structure.StructuralType.Column);
                        
                        // Set offsets
                        Parameter baseOffset = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                        if (baseOffset != null && !baseOffset.IsReadOnly)
                            baseOffset.Set(Payload.OffsetBot);

                        Parameter topLevel = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                        if (topLevel != null && !topLevel.IsReadOnly && Payload.LevelTop != null)
                            topLevel.Set(Payload.LevelTop.Id);

                        Parameter topOffset = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                        if (topOffset != null && !topOffset.IsReadOnly)
                            topOffset.Set(Payload.OffsetTop);
                    }

                    tx.Commit();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled pick object
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName()
        {
            return "Auto Column Event Handler";
        }
    }
}

