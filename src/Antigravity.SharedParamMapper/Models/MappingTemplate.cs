using System;
using System.Collections.Generic;

namespace Antigravity.SharedParamMapper.Models
{
    public class MappingTemplate
    {
        public int Version { get; set; } = 1;
        public string ProjectName { get; set; }
        public DateTime LastModified { get; set; }
        public List<FamilyTypeMapping> Mappings { get; set; } = new List<FamilyTypeMapping>();
    }
}
