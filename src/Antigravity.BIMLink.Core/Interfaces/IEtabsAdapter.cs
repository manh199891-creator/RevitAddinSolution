using System.Collections.Generic;
using System.Threading.Tasks;
using Antigravity.BIMLink.Core.Models;

namespace Antigravity.BIMLink.Core.Interfaces
{
    public interface IEtabsAdapter
    {
        Task ConnectAsync();

        Task PushModelAsync(IEnumerable<StructuralElementDTO> elements);

        Task<IEnumerable<StructuralElementDTO>> PullInternalForcesAsync(string loadComboName);
    }
}
