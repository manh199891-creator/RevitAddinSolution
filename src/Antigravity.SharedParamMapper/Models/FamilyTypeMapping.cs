using System.Collections.Generic;

namespace Antigravity.SharedParamMapper.Models
{
    public class FamilyTypeMapping
    {
        public string CategoryName { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public int ElementTypeId { get; set; }
        public List<MappingRule> Rules { get; set; } = new List<MappingRule>();
    }
}
