using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Antigravity.HoanThien.Services.Identity
{
    public interface IIdentityManager
    {
        void StampIdentity(Document doc, ElementId finishWallId, ElementId roomId, ElementId hostWallId);
        void CleanupExistingFinishes(Document doc, ElementId roomId);
    }

    public class IdentityManager : IIdentityManager
    {
        // Parameter names that should exist in the project as Shared Parameters
        private const string RoomIdParamName = "AG_RoomId";
        private const string HostIdParamName = "AG_HostId";

        public void StampIdentity(Document doc, ElementId finishWallId, ElementId roomId, ElementId hostWallId)
        {
            var wall = doc.GetElement(finishWallId);
            if (wall == null) return;

            SetParameter(wall, RoomIdParamName, roomId.ToString());
            SetParameter(wall, HostIdParamName, hostWallId.ToString());
        }

        public void CleanupExistingFinishes(Document doc, ElementId roomId)
        {
            var roomIdStr = roomId.ToString();

            // Find all walls that have the AG_RoomId matching this room
            var existingFinishes = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Where(e => 
                {
                    var param = e.LookupParameter(RoomIdParamName);
                    return param != null && param.AsString() == roomIdStr;
                })
                .Select(e => e.Id)
                .ToList();

            if (existingFinishes.Any())
            {
                doc.Delete(existingFinishes);
            }
        }

        private void SetParameter(Element element, string paramName, string value)
        {
            var param = element.LookupParameter(paramName);
            if (param != null && !param.IsReadOnly)
            {
                param.Set(value);
            }
        }
    }
}
