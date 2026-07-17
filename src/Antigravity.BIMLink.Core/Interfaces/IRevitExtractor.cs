using System.Collections.Generic;
using System.Threading.Tasks;
using Antigravity.BIMLink.Core.Models;

namespace Antigravity.BIMLink.Core.Interfaces
{
    public interface IRevitExtractor
    {
        Task<IEnumerable<StructuralElementDTO>> ExtractAnalyticalModelAsync();
        
        Task UpdateInternalForcesAsync(IEnumerable<StructuralElementDTO> elementsWithForces);
    }
}
