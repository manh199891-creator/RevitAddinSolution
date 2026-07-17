using Autodesk.Revit.DB;
using Antigravity.HoanThien.Models;

namespace Antigravity.HoanThien.Services.Generation
{
    public interface IFinishGenerator
    {
        void Generate(Document doc, FinishPlan plan);
    }

    public class FinishGenerator : IFinishGenerator
    {
        private readonly IWallGenerationService _wallGenerator;
        private readonly IFinishJoinManager _joinManager;
        private readonly IOpeningHandler _openingHandler;
        private readonly Antigravity.HoanThien.Services.Identity.IIdentityManager _identityManager;

        public FinishGenerator(IWallGenerationService wallGenerator, IFinishJoinManager joinManager, IOpeningHandler openingHandler, Antigravity.HoanThien.Services.Identity.IIdentityManager identityManager)
        {
            _wallGenerator = wallGenerator;
            _joinManager = joinManager;
            _openingHandler = openingHandler;
            _identityManager = identityManager;
        }

        public void Generate(Document doc, FinishPlan plan)
        {
            using (var t = new Transaction(doc, "Generate Finishes"))
            {
                t.Start();

                foreach (var room in plan.Rooms)
                {
                    if (room.Status == PlanStatus.Error) continue;

                    // PHASE 4: CLEANUP existing finish walls for this room to avoid duplicates
                    _identityManager.CleanupExistingFinishes(doc, room.RoomId);

                    foreach (var surface in room.Surfaces)
                    {
                        foreach (var rule in surface.AppliedRules)
                        {
                            var wallId = _wallGenerator.GenerateWall(doc, surface, rule, room.Room.Level);
                            
                            if (wallId != ElementId.InvalidElementId && surface.HostElementId != ElementId.InvalidElementId)
                            {
                                _joinManager.JoinFinishToHost(doc, wallId, surface.HostElementId);
                                _openingHandler.CutOpenings(doc, wallId, surface.HostElementId);
                                
                                // PHASE 4: STAMP identity parameters
                                _identityManager.StampIdentity(doc, wallId, room.RoomId, surface.HostElementId);
                            }
                        }
                    }
                }

                t.Commit();
            }
        }
    }
}
