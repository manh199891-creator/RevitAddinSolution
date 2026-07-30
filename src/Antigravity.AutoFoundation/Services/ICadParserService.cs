using System.Collections.Generic;
using Antigravity.AutoFoundation.Models;

namespace Antigravity.AutoFoundation.Services
{
    public interface ICadParserService
    {
        List<FoundationData> ExtractFoundationData(object cadLink, string layerName);
    }
}
