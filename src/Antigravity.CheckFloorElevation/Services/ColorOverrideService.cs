using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.CheckFloorElevation.Models;
using Antigravity.Core.Services;

namespace Antigravity.CheckFloorElevation.Services
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

        public void ApplyOverrides(IEnumerable<FloorCheckResult> results)
        {
            if (results == null)
                return;

            ElementId solidFillPatternId = GetSolidFillPatternId();

            using (var transaction = new Transaction(_doc, "Apply floor elevation check colors"))
            {
                transaction.Start();

                foreach (FloorCheckResult result in results.Where(r => r.IsError || r.IsNoMatch))
                {
                    ElementId id = new ElementId((long)result.HostFloorId);
                    if (_doc.GetElement(id) == null)
                        continue;

                    var settings = new OverrideGraphicSettings();
                    if (solidFillPatternId != ElementId.InvalidElementId)
                    {
                        settings.SetSurfaceForegroundPatternId(solidFillPatternId);
                        settings.SetSurfaceForegroundPatternColor(result.IsError
                            ? new Color(220, 38, 38)
                            : new Color(245, 158, 11));
                    }

                    settings.SetProjectionLineColor(result.IsError
                        ? new Color(185, 28, 28)
                        : new Color(217, 119, 6));

                    _view.SetElementOverrides(id, settings);
                }

                transaction.Commit();
            }
        }

        public void ResetOverrides(IEnumerable<FloorCheckResult> results)
        {
            if (results == null)
                return;

            using (var transaction = new Transaction(_doc, "Reset floor elevation check colors"))
            {
                transaction.Start();

                foreach (FloorCheckResult result in results)
                {
                    ElementId id = new ElementId((long)result.HostFloorId);
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
                AppLogger.Error(ex, "[CheckFloorElevation] Cannot find solid fill pattern");
                return ElementId.InvalidElementId;
            }
        }
    }
}
