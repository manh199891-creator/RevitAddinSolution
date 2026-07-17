using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Antigravity.BIMLink.Core.Interfaces;
using Antigravity.BIMLink.Core.Models;
// using ETABSv1;

namespace Antigravity.BIMLink.Etabs
{
    public class EtabsAdapter : IEtabsAdapter
    {
        // cOAPI _myEtabsObject;
        // cSapModel _sapModel;

        public Task ConnectAsync()
        {
            // TODO: Connect to running ETABS instance or start a new one via COM
            return Task.CompletedTask;
        }

        public Task PushModelAsync(IEnumerable<StructuralElementDTO> elements)
        {
            // TODO: Loop through elements and call _sapModel.FrameObj.AddByCoord etc.
            return Task.CompletedTask;
        }

        public Task<IEnumerable<StructuralElementDTO>> PullInternalForcesAsync(string loadComboName)
        {
            // TODO: Retrieve forces from ETABS results and map back to DTO
            IEnumerable<StructuralElementDTO> result = new List<StructuralElementDTO>();
            return Task.FromResult(result);
        }
    }
}
