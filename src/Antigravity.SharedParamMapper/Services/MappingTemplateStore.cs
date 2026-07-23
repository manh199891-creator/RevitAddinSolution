using System;
using System.Collections.Generic;
using System.IO;
using Antigravity.SharedParamMapper.Models;
using Newtonsoft.Json;

namespace Antigravity.SharedParamMapper.Services
{
    public class MappingTemplateStore
    {
        private readonly string _storageDir;

        public MappingTemplateStore()
        {
            _storageDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Antigravity",
                "ParamMapper"
            );

            if (!Directory.Exists(_storageDir))
            {
                Directory.CreateDirectory(_storageDir);
            }
        }

        public void Save(MappingTemplate template)
        {
            template.LastModified = DateTime.Now;
            string filePath = GetFilePath(template.ProjectName);
            string json = JsonConvert.SerializeObject(template, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public MappingTemplate Load(string projectName)
        {
            string filePath = GetFilePath(projectName);
            if (!File.Exists(filePath))
            {
                return new MappingTemplate { ProjectName = projectName };
            }

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<MappingTemplate>(json) ?? new MappingTemplate { ProjectName = projectName };
        }

        public List<string> ListTemplates()
        {
            var templates = new List<string>();
            if (Directory.Exists(_storageDir))
            {
                var files = Directory.GetFiles(_storageDir, "*.json");
                foreach (var file in files)
                {
                    templates.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            return templates;
        }

        private string GetFilePath(string projectName)
        {
            // Sanitize project name for file path
            var invalidChars = Path.GetInvalidFileNameChars();
            string sanitizedName = string.Join("_", projectName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return Path.Combine(_storageDir, $"{sanitizedName}.json");
        }
    }
}
