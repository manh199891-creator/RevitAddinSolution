using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Antigravity.ZoneSplit.Services
{
    public static class ParameterSetupService
    {
        public const string PARAM_ZONE_ID = "BIM_ZoneID";
        public const string PARAM_ZONE_NAME = "BIM_ZoneName";
        public const string PARAM_VOLUME_PREFIX = "BIM_";
        private const string SharedParameterFileName = "VilaiViet_ZoneSplit_SharedParams_v2.txt";
        private const string SharedParameterGuidNamespace = "VILAIVIET_ZONE_SPLIT";

        private static readonly BuiltInCategory[] DefaultTargetCategories =
        {
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Parts
        };

        public static void EnsureZoneParametersExist(Document doc, List<BuiltInCategory> targetCategories)
        {
            if (doc == null || doc.IsFamilyDocument) return;

            var categories = targetCategories != null && targetCategories.Count > 0
                ? targetCategories.Distinct().ToList()
                : DefaultTargetCategories.ToList();

            using (var tx = new Transaction(doc, "Setup Zone Parameters"))
            {
                string originalFile = doc.Application.SharedParametersFilename;

                tx.Start();
                try
                {
                    string tempSharedParamFile = CreateTempSharedParameterFile();
                    doc.Application.SharedParametersFilename = tempSharedParamFile;

                    var definitionFile = doc.Application.OpenSharedParameterFile();
                    if (definitionFile == null) return;

                    var group = definitionFile.Groups.get_Item("VILAIVIET_ZONE")
                             ?? definitionFile.Groups.Create("VILAIVIET_ZONE");

                    BindParameter(doc, GetOrCreateTextDefinition(group, PARAM_ZONE_ID), categories);
                    BindParameter(doc, GetOrCreateTextDefinition(group, PARAM_ZONE_NAME), categories);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"Error setting up zone parameters: {ex}");
                }
                finally
                {
                    doc.Application.SharedParametersFilename = originalFile;
                    if (tx.HasStarted())
                    {
                        tx.Commit();
                    }
                }
            }

            CleanupOldParameters(doc);
        }

        private static void CleanupOldParameters(Document doc)
        {
            using (var tx = new Transaction(doc, "Cleanup Old Zone Parameters"))
            {
                tx.Start();
                var iterator = doc.ParameterBindings.ForwardIterator();
                var definitionsToRemove = new List<Definition>();
                while (iterator.MoveNext())
                {
                    var def = iterator.Key;
                    if (def != null)
                    {
                        bool isOldVol = def.Name.StartsWith("Vol_", StringComparison.OrdinalIgnoreCase);
                        bool isOldVolumeBim = def.Name.StartsWith("Volume_BIM_", StringComparison.OrdinalIgnoreCase);

                        if (isOldVol || isOldVolumeBim)
                        {
                            definitionsToRemove.Add(def);
                        }
                    }
                }
                foreach (var def in definitionsToRemove)
                {
                    doc.ParameterBindings.Remove(def);
                }
                tx.Commit();
            }
        }

        public static void EnsureDynamicZoneVolumeParameters(
            Document doc,
            IEnumerable<string> zoneNames,
            List<BuiltInCategory> targetCategories)
        {
            if (doc == null || doc.IsFamilyDocument) return;

            var paramNames = (zoneNames ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(GetVolumeParameterName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (paramNames.Count == 0) return;

            var categories = targetCategories != null && targetCategories.Count > 0
                ? targetCategories.Distinct().ToList()
                : DefaultTargetCategories.ToList();

            using (var tx = new Transaction(doc, "Setup Zone Volume Parameters"))
            {
                string originalFile = doc.Application.SharedParametersFilename;

                tx.Start();
                try
                {
                    RemoveConflictingSharedParameters(doc, paramNames, SpecTypeId.Volume);

                    string tempSharedParamFile = CreateTempSharedParameterFile();
                    doc.Application.SharedParametersFilename = tempSharedParamFile;

                    var definitionFile = doc.Application.OpenSharedParameterFile();
                    if (definitionFile == null) return;

                    var group = definitionFile.Groups.get_Item("VILAIVIET_ZONE")
                             ?? definitionFile.Groups.Create("VILAIVIET_ZONE");

                    foreach (var paramName in paramNames)
                    {
                        BindParameter(doc, GetOrCreateVolumeDefinition(group, paramName), categories);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"Error setting up dynamic zone volume parameters: {ex}");
                }
                finally
                {
                    doc.Application.SharedParametersFilename = originalFile;
                    if (tx.HasStarted())
                    {
                        tx.Commit();
                    }
                }
            }
        }

        public static string GetVolumeParameterName(string zoneName)
        {
            var cleaned = new string((zoneName ?? string.Empty)
                .Select(ch => char.IsControl(ch) || ch == '\t' ? '_' : ch)
                .ToArray())
                .Trim();

            return PARAM_VOLUME_PREFIX + (string.IsNullOrWhiteSpace(cleaned) ? "Unknown" : cleaned);
        }

        public static Guid GetSharedParameterGuid(string paramName)
        {
            return CreateStableGuid(paramName);
        }

        private static Definition GetOrCreateTextDefinition(DefinitionGroup group, string paramName)
        {
            var def = group.Definitions.get_Item(paramName);
            if (def != null) return def;

            var opt = new ExternalDefinitionCreationOptions(paramName, SpecTypeId.String.Text)
            {
                GUID = CreateStableGuid(paramName)
            };
            return group.Definitions.Create(opt);
        }

        private static Definition GetOrCreateVolumeDefinition(DefinitionGroup group, string paramName)
        {
            var def = group.Definitions.get_Item(paramName);
            if (def != null) return def;

            var opt = new ExternalDefinitionCreationOptions(paramName, SpecTypeId.Volume)
            {
                GUID = CreateStableGuid(paramName),
                HideWhenNoValue = true
            };
            return group.Definitions.Create(opt);
        }

        private static void RemoveConflictingSharedParameters(
            Document doc,
            IEnumerable<string> parameterNames,
            ForgeTypeId expectedType)
        {
            var names = new HashSet<string>(
                parameterNames ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            if (names.Count == 0) return;

            var iterator = doc.ParameterBindings.ForwardIterator();
            var definitionsToRemove = new List<Definition>();

            while (iterator.MoveNext())
            {
                var def = iterator.Key;
                if (def == null || !names.Contains(def.Name)) continue;

                if (!def.GetDataType().Equals(expectedType) || HasConflictingSharedParameterGuid(doc, def.Name))
                {
                    definitionsToRemove.Add(def);
                }
            }

            foreach (var def in definitionsToRemove)
            {
                doc.ParameterBindings.Remove(def);
            }

            var sharedParametersToDelete = new FilteredElementCollector(doc)
                .OfClass(typeof(SharedParameterElement))
                .Cast<SharedParameterElement>()
                .Where(x => names.Contains(x.Name) && x.GuidValue != CreateStableGuid(x.Name))
                .Select(x => x.Id)
                .ToList();

            if (sharedParametersToDelete.Count > 0)
            {
                doc.Delete(sharedParametersToDelete);
            }
        }

        private static bool HasConflictingSharedParameterGuid(Document doc, string parameterName)
        {
            var expectedGuid = CreateStableGuid(parameterName);

            var matchingSharedParameters = new FilteredElementCollector(doc)
                .OfClass(typeof(SharedParameterElement))
                .Cast<SharedParameterElement>()
                .Where(x => x.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return matchingSharedParameters.Count == 0
                || matchingSharedParameters.Any(x => x.GuidValue != expectedGuid);
        }

        private static void BindParameter(Document doc, Definition def, List<BuiltInCategory> categories)
        {
            var categorySet = doc.Application.Create.NewCategorySet();
            foreach (var bic in categories)
            {
                var cat = Category.GetCategory(doc, bic);
                if (cat != null)
                {
                    categorySet.Insert(cat);
                }
            }

            if (categorySet.IsEmpty) return;

            var binding = doc.Application.Create.NewInstanceBinding(categorySet);
            if (!doc.ParameterBindings.Insert(def, binding, GroupTypeId.IdentityData))
            {
                doc.ParameterBindings.ReInsert(def, binding, GroupTypeId.IdentityData);
            }
        }

        private static string CreateTempSharedParameterFile()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), SharedParameterFileName);
            if (File.Exists(tempPath)) return tempPath;

            using (var sw = new StreamWriter(tempPath))
            {
                sw.WriteLine("# This is a Revit shared parameter file.");
                sw.WriteLine("# Do not edit manually.");
                sw.WriteLine("*META\tVERSION\tMINVERSION");
                sw.WriteLine("META\t2\t1");
                sw.WriteLine("*GROUP\tID\tNAME");
                sw.WriteLine("*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNOVALUE");
            }

            return tempPath;
        }

        private static Guid CreateStableGuid(string parameterName)
        {
            using (var md5 = MD5.Create())
            {
                var key = $"{SharedParameterGuidNamespace}:{parameterName ?? string.Empty}".ToUpperInvariant();
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
                return new Guid(hash);
            }
        }
    }
}
