using System;
using System.Collections.Generic;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DoorClearanceBox.Updaters;

namespace DoorClearanceBox
{
    /// <summary>
    /// Door clearance add-in entry point.
    /// Registers the updater only; ribbon UI is owned by Antigravity.Main.
    /// </summary>
    public class App : IExternalApplication
    {
        public static App Instance { get; private set; }

        private DoorChangeUpdater _updater;

        public Result OnStartup(UIControlledApplication application)
        {
            Instance = this;

            try
            {
                RegisterUpdater(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(Constants.AddInName, $"Failed to initialise add-in:\n{ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (_updater != null)
                    UpdaterRegistry.UnregisterUpdater(_updater.GetUpdaterId());
            }
            catch
            {
                // Revit may already be shutting down.
            }

            return Result.Succeeded;
        }

        private void RegisterUpdater(UIControlledApplication app)
        {
            _updater = new DoorChangeUpdater(app.ActiveAddInId);
            UpdaterRegistry.RegisterUpdater(_updater, true);

            var categories = new List<ElementId>
            {
                new ElementId(BuiltInCategory.OST_Doors),
                new ElementId(BuiltInCategory.OST_Windows),
                new ElementId(BuiltInCategory.OST_CurtainWallPanels)
            };
            var elementFilter = new ElementMulticategoryFilter(categories);

            UpdaterRegistry.AddTrigger(
                _updater.GetUpdaterId(),
                elementFilter,
                Element.GetChangeTypeParameter(new ElementId(BuiltInParameter.FAMILY_WIDTH_PARAM)));

            UpdaterRegistry.AddTrigger(
                _updater.GetUpdaterId(),
                elementFilter,
                Element.GetChangeTypeParameter(new ElementId(BuiltInParameter.FAMILY_HEIGHT_PARAM)));
        }
    }

    /// <summary>
    /// Controls when ribbon buttons are enabled.
    /// Buttons are active only when a non-read-only document is open.
    /// </summary>
    public sealed class CommandAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication app, CategorySet selectedCategories)
        {
            var doc = app.ActiveUIDocument?.Document;
            return doc != null && !doc.IsReadOnly;
        }
    }
}
