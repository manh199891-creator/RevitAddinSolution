using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.CheckFloorElevation.Services
{
    public class FloorCollectorService
    {
        private readonly Document _hostDoc;

        public FloorCollectorService(Document hostDoc)
        {
            _hostDoc = hostDoc;
        }

        public IList<Floor> GetHostFloors(View activeView, bool activeViewOnly, IList<ElementId> levelIds = null)
        {
            FilteredElementCollector collector = activeViewOnly && activeView != null
                ? new FilteredElementCollector(_hostDoc, activeView.Id)
                : new FilteredElementCollector(_hostDoc);

            IEnumerable<Floor> floors = collector
                .OfClass(typeof(Floor))
                .WhereElementIsNotElementType()
                .Cast<Floor>();

            if (levelIds != null && levelIds.Count > 0)
                floors = floors.Where(floor => levelIds.Any(levelId => levelId == floor.LevelId));

            return floors
                .ToList();
        }

        public IList<Level> GetHostLevels()
        {
            return new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => level.Elevation)
                .ThenBy(level => level.Name)
                .ToList();
        }

        public IList<RevitLinkInstance> GetLoadedLinks()
        {
            return new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(link => link.GetLinkDocument() != null)
                .OrderBy(link => link.Name)
                .ToList();
        }

        public View3D FindSuitable3DView(View activeView = null)
        {
            View3D active3D = activeView as View3D;
            if (active3D != null && !active3D.IsTemplate)
                return active3D;

            return new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(view => !view.IsTemplate && view.CanBePrinted)
                .OrderBy(view => view.Name)
                .FirstOrDefault();
        }
    }
}
