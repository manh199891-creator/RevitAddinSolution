using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DoorClearanceBox.Core;

namespace DoorClearanceBox.Updaters
{
    /// <summary>
    /// DMU Updater — Layer responsible for detecting door dimension changes
    /// and flagging the associated clearance DirectShapes as stale.
    ///
    /// Why IUpdater (not DocumentChanged event):
    ///   IUpdater runs synchronously inside the modifying transaction,
    ///   ensuring the stale flag is written atomically with the door edit.
    ///   DocumentChanged fires post-transaction and cannot modify the document
    ///   without a new transaction (which may be disallowed in some Revit states).
    ///
    /// Triggers registered in App.OnStartup:
    ///   • FAMILY_WIDTH_PARAM changed on any OST_Doors element
    ///   • FAMILY_HEIGHT_PARAM changed on any OST_Doors element
    /// </summary>
    public sealed class DoorChangeUpdater : IUpdater
    {
        private readonly UpdaterId _updaterId;

        public DoorChangeUpdater(AddInId addInId)
        {
            _updaterId = new UpdaterId(addInId, new Guid(Constants.UpdaterGuid));
        }

        // ── IUpdater ───────────────────────────────────────────────────────────

        public void Execute(UpdaterData data)
        {
            var doc = data.GetDocument();

            // Collect door IDs whose dimensions changed
            var changedDoorIds = new HashSet<ElementId>(
                data.GetModifiedElementIds());

            if (changedDoorIds.Count == 0) return;

            // Find DirectShapes linked to these doors and flag each as stale.
            // Use SubTransaction: we are already inside Revit's transaction.
            TransactionWrapper.ExecuteSub(doc, () =>
            {
                var directShapesByDoorId = ReserveSpaceGeometry.FindForElements(doc, changedDoorIds);
                foreach (var ds in directShapesByDoorId.Values)
                    if (ds != null)
                        ReserveSpaceGeometry.MarkAsStale(ds);
            });
        }

        public UpdaterId    GetUpdaterId()           => _updaterId;
        public ChangePriority GetChangePriority()    => ChangePriority.Annotations;
        public string       GetUpdaterName()         => "Door Clearance Box Stale Flagger";
        public string       GetAdditionalInformation() =>
            "Flags clearance DirectShapes when source door width or height is modified.";
    }
}
