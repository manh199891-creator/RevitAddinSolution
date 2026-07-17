using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.WallMepClash.Services
{
    public class WallCollectorService
    {
        private readonly Document _hostDoc;

        public WallCollectorService(Document hostDoc)
        {
            _hostDoc = hostDoc;
        }

        public IList<Wall> GetHostWalls(View activeView, bool activeViewOnly, IList<ElementId> levelIds = null)
        {
            FilteredElementCollector collector = activeViewOnly && activeView != null
                ? new FilteredElementCollector(_hostDoc, activeView.Id)
                : new FilteredElementCollector(_hostDoc);

            IEnumerable<Wall> walls = collector
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>();

            if (levelIds != null && levelIds.Count > 0)
            {
                walls = walls.Where(wall => levelIds.Any(levelId => levelId == wall.LevelId));
            }

            return walls.ToList();
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
