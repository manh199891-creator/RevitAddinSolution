using Autodesk.Revit.DB;

namespace Antigravity.HoanThien.Services.Generation
{
    public interface IFinishJoinManager
    {
        void JoinFinishToHost(Document doc, ElementId finishWallId, ElementId hostWallId);
    }

    public class FinishJoinManager : IFinishJoinManager
    {
        public void JoinFinishToHost(Document doc, ElementId finishWallId, ElementId hostWallId)
        {
            var finishWall = doc.GetElement(finishWallId);
            var hostWall = doc.GetElement(hostWallId);

            if (finishWall == null || hostWall == null) return;

            // Use JoinGeometryUtils to join the new finish wall to the host wall
            if (!JoinGeometryUtils.AreElementsJoined(doc, finishWall, hostWall))
            {
                JoinGeometryUtils.JoinGeometry(doc, finishWall, hostWall);
            }
        }
    }
}
