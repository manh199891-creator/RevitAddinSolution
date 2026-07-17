using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.WallMepClash.Models;
using Antigravity.Core.Services;

namespace Antigravity.WallMepClash.Services
{
    public class ColorOverrideService
    {
        private readonly Document _doc;
        private readonly View _view;

        public ColorOverrideService(Document doc, View view)
        {
            _doc = doc;
            _view = view;
        }

        public void ApplyOverrides(IEnumerable<ClashResult> results)
        {
            if (results == null)
                return;

            ElementId solidFillPatternId = GetSolidFillPatternId();

            using (var transaction = new Transaction(_doc, "Apply wall clash colors"))
            {
                transaction.Start();

                foreach (ClashResult result in results)
                {
                    ElementId id = new ElementId((long)result.HostWallId);
                    if (_doc.GetElement(id) == null)
                        continue;

                    var settings = new OverrideGraphicSettings();
                    if (solidFillPatternId != ElementId.InvalidElementId)
                    {
                        settings.SetSurfaceForegroundPatternId(solidFillPatternId);
                        settings.SetSurfaceForegroundPatternColor(new Color(220, 38, 38)); // Màu đỏ
                    }

                    settings.SetProjectionLineColor(new Color(185, 28, 28));

                    _view.SetElementOverrides(id, settings);
                }

                transaction.Commit();
            }
        }

        public void ResetOverrides(IEnumerable<ClashResult> results)
        {
            if (results == null)
                return;

            using (var transaction = new Transaction(_doc, "Reset wall clash colors"))
            {
                transaction.Start();

                foreach (ClashResult result in results)
                {
                    ElementId id = new ElementId((long)result.HostWallId);
                    if (_doc.GetElement(id) != null)
                        _view.SetElementOverrides(id, new OverrideGraphicSettings());
                }

                transaction.Commit();
            }
        }

        private ElementId GetSolidFillPatternId()
        {
            try
            {
                FillPatternElement fill = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(pattern => pattern.GetFillPattern().IsSolidFill);

                return fill == null ? ElementId.InvalidElementId : fill.Id;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "[WallMepClash] Cannot find solid fill pattern");
                return ElementId.InvalidElementId;
            }
        }
    }
}
