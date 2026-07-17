using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Antigravity.BIMLink.Core.Interfaces;

namespace Antigravity.BIMLink.Core.Services
{
    public class MappingProfile
    {
        public Dictionary<string, string> SectionMappings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> MaterialMappings { get; set; } = new Dictionary<string, string>();
    }

    public class MaterialSectionMapper : IMaterialSectionMapper
    {
        private MappingProfile _profile;

        public MaterialSectionMapper()
        {
            _profile = new MappingProfile();
        }

        public void LoadMappingProfile(string configFilePath)
        {
            if (File.Exists(configFilePath))
            {
                var json = File.ReadAllText(configFilePath);
                _profile = JsonSerializer.Deserialize<MappingProfile>(json);
            }
        }

        public string GetEtabsSectionName(string revitFamily, string revitType)
        {
            string key = $"{revitFamily}::{revitType}";
            if (_profile.SectionMappings.TryGetValue(key, out var etabsSection))
            {
                return etabsSection;
            }
            return revitType; // Fallback to Revit Type Name
        }

        public string GetEtabsMaterialName(string revitMaterial)
        {
            if (_profile.MaterialMappings.TryGetValue(revitMaterial, out var etabsMaterial))
            {
                return etabsMaterial;
            }
            return revitMaterial;
        }
    }
}
