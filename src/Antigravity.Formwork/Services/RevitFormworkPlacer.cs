using System;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.Formwork.Core.Models;

namespace Antigravity.Formwork.Services
{
    public class RevitFormworkPlacer
    {
        private readonly Document _doc;
        private FamilySymbol _genericPanelType;

        public RevitFormworkPlacer(Document doc)
        {
            _doc = doc;
            LoadOrFindPanelType();
        }

        private void LoadOrFindPanelType()
        {
            // Try to find a generic formwork panel family.
            // For MVP, we look for a family named "Generic_Formwork_Panel" or similar, or just any Generic Model if none found for testing.
            _genericPanelType = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.FamilyName.Contains("Formwork") || s.FamilyName.Contains("Panel"));
            
            // Note: If no such family exists, the UI will warn the user or the command will fail gracefully.
        }

        public bool CanPlace => _genericPanelType != null;

        public void PlacePanels(PlacementResult result)
        {
            if (!CanPlace) return;

            using (var tx = new Transaction(_doc, "Place Automatic Formwork"))
            {
                tx.Start();

                if (!_genericPanelType.IsActive)
                    _genericPanelType.Activate();

                // Find a default level (e.g. level 1) to host the family instances
                var level = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault();

                foreach (var panel in result.PlacedPanels)
                {
                    // Convert millimeters to feet for Revit API
                    double xFt = panel.CenterX / 304.8;
                    double yFt = panel.CenterY / 304.8;
                    double zFt = panel.CenterZ / 304.8;
                    
                    var center = new XYZ(xFt, yFt, zFt);

                    var instance = _doc.Create.NewFamilyInstance(center, _genericPanelType, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                    // Rotate the panel
                    var axis = Line.CreateBound(center, center + XYZ.BasisZ);
                    ElementTransformUtils.RotateElement(_doc, instance.Id, axis, panel.RotationAngle);

                    // Set Metadata (HostUniqueId, CycleId, RunId)
                    SetParameter(instance, "FormworkHostId", result.HostUniqueId);
                    SetParameter(instance, "FormworkHostCategory", result.HostCategory);
                    SetParameter(instance, "FormworkCycleId", result.CycleId);
                    SetParameter(instance, "FormworkPanelWidth", panel.Width);
                    SetParameter(instance, "FormworkPanelHeight", panel.Height);
                    SetParameter(instance, "IsFiller", panel.IsFiller ? 1 : 0);
                }

                tx.Commit();
            }
        }

        private void SetParameter(FamilyInstance instance, string paramName, object value)
        {
            var param = instance.LookupParameter(paramName);
            if (param != null && !param.IsReadOnly)
            {
                if (value is string s) param.Set(s);
                else if (value is double d) param.Set(d / 304.8); // Simple assumption for length parameters
                else if (value is int i) param.Set(i);
            }
        }
    }
}
