using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using Antigravity.IssueManager.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace Antigravity.IssueManager.Services
{
    public static class IssueStorageService
    {
        private static readonly Guid SchemaGuid = new Guid("7B13F48C-21C5-47B2-8C7F-7F4F52F0B39C");
        private const string SchemaName = "Antigravity_IssueManager_Schema";
        private const string IssuesJsonFieldName = "IssuesJson";

        public static void SaveIssuesToDocument(Document doc, List<IssueModel> issues)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            Schema schema = GetOrCreateSchema();
            Field field = schema.GetField(IssuesJsonFieldName);
            DataStorage storage = GetOrCreateDataStorage(doc, schema);

            Entity entity = storage.GetEntity(schema);
            if (!entity.IsValid())
            {
                entity = new Entity(schema);
            }

            entity.Set(field, SerializeIssues(issues ?? new List<IssueModel>()));
            storage.SetEntity(entity);
        }

        public static List<IssueModel> LoadIssuesFromDocument(Document doc)
        {
            if (doc == null) return new List<IssueModel>();

            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema == null) return new List<IssueModel>();

            Field field = schema.GetField(IssuesJsonFieldName);
            if (field == null) return new List<IssueModel>();

            DataStorage storage = FindDataStorage(doc, schema);
            if (storage == null) return new List<IssueModel>();

            Entity entity = storage.GetEntity(schema);
            if (!entity.IsValid()) return new List<IssueModel>();

            string json = entity.Get<string>(field);
            if (string.IsNullOrWhiteSpace(json)) return new List<IssueModel>();

            return DeserializeIssues(json);
        }

        private static Schema GetOrCreateSchema()
        {
            Schema existing = Schema.Lookup(SchemaGuid);
            if (existing != null)
            {
                return existing;
            }

            SchemaBuilder builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(SchemaName);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(IssuesJsonFieldName, typeof(string));
            return builder.Finish();
        }

        private static DataStorage GetOrCreateDataStorage(Document doc, Schema schema)
        {
            DataStorage storage = FindDataStorage(doc, schema);
            return storage ?? DataStorage.Create(doc);
        }

        private static DataStorage FindDataStorage(Document doc, Schema schema)
        {
            foreach (DataStorage storage in new FilteredElementCollector(doc).OfClass(typeof(DataStorage)))
            {
                Entity entity = storage.GetEntity(schema);
                if (entity.IsValid())
                {
                    return storage;
                }
            }

            return null;
        }

        public static string SerializeIssues(List<IssueModel> issues)
        {
            if (issues != null)
            {
                foreach (var issue in issues)
                {
                    if (issue.CreationDate < new DateTime(1970, 1, 1))
                    {
                        issue.CreationDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    }
                    else if (issue.CreationDate.Kind == DateTimeKind.Local)
                    {
                        try
                        {
                            issue.CreationDate = issue.CreationDate.ToUniversalTime();
                        }
                        catch
                        {
                            issue.CreationDate = DateTime.SpecifyKind(issue.CreationDate, DateTimeKind.Utc);
                        }
                    }
                }
            }

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<IssueModel>));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, issues);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static List<IssueModel> DeserializeIssues(string json)
        {
            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<IssueModel>));
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return serializer.ReadObject(stream) as List<IssueModel> ?? new List<IssueModel>();
                }
            }
            catch
            {
                return new List<IssueModel>();
            }
        }
    }
}
