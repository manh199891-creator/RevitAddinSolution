using System.Collections.Generic;
using Antigravity.Core.Models;

namespace Antigravity.Core.Services
{
    public interface ICadParserService
    {
        List<FoundationData> ExtractFoundationData(object cadLink, string layerName);
    }
}
