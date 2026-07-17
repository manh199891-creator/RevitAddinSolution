namespace Antigravity.BIMLink.Core.Interfaces
{
    public interface IMaterialSectionMapper
    {
        string GetEtabsSectionName(string revitFamily, string revitType);
        string GetEtabsMaterialName(string revitMaterial);
        void LoadMappingProfile(string configFilePath);
    }
}
