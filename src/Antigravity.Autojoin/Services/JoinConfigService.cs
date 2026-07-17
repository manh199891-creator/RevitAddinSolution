using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Antigravity.Autojoin.Models;
using Antigravity.Core.Services;

namespace Antigravity.Autojoin.Services
{
    public static class JoinConfigService
    {
        private static readonly string ConfigDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "Antigravity", "AutoJoin");

        private static readonly string ConfigPath =
            Path.Combine(ConfigDir, "rules.json");

        // ── Defaults ──────────────────────────────────────────────────────

        /// <summary>Cặp mặc định: Dầm→Sàn, Dầm→Cột, Cột→Sàn</summary>
        public static List<JoinRule> DefaultRules() => new List<JoinRule>
        {
            new JoinRule { Order = 1, CategoryA = "OST_StructuralFraming",  CategoryB = "OST_Floors",            IsEnabled = true },
            new JoinRule { Order = 2, CategoryA = "OST_StructuralFraming",  CategoryB = "OST_StructuralColumns", IsEnabled = true },
            new JoinRule { Order = 3, CategoryA = "OST_StructuralColumns",  CategoryB = "OST_Floors",            IsEnabled = true },
            new JoinRule { Order = 4, CategoryA = "OST_StructuralColumns",  CategoryB = "OST_Walls",             IsEnabled = true },
            new JoinRule { Order = 5, CategoryA = "OST_Walls",              CategoryB = "OST_StructuralFraming", IsEnabled = true },
            new JoinRule { Order = 6, CategoryA = "OST_Walls",              CategoryB = "OST_Floors",            IsEnabled = true },
            new JoinRule { Order = 7, CategoryA = "OST_StructuralColumns",  CategoryB = "OST_StructuralFoundation", IsEnabled = true },
            new JoinRule { Order = 8, CategoryA = "OST_Walls",              CategoryB = "OST_StructuralFoundation", IsEnabled = true },
        };

        // ── Load / Save ──────────────────────────────────────────────────

        public static List<JoinRule> Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Save(DefaultRules());
                    return DefaultRules();
                }

                var json = File.ReadAllText(ConfigPath);
                var list = DeserializeRules(json);
                if (list == null) return DefaultRules();

                return EnsureDefaultRulesPresent(list);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Lỗi đọc file cấu hình:\n{ex.Message}\n\nSử dụng cấu hình mặc định.",
                    "AutoJoin - Cảnh báo",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return DefaultRules();
            }
        }

        public static void Save(List<JoinRule> rules)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var json = SerializeRules(rules);
                File.WriteAllText(ConfigPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Lỗi lưu file cấu hình:\n{ex.Message}",
                    "AutoJoin - Lỗi",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        // ── DMU state ────────────────────────────────────────────────────

        private static List<JoinRule> EnsureDefaultRulesPresent(List<JoinRule> rules)
        {
            bool changed = false;

            foreach (var defaultRule in DefaultRules())
            {
                bool exists = rules.Exists(r =>
                    r.CategoryA == defaultRule.CategoryA &&
                    r.CategoryB == defaultRule.CategoryB);

                if (exists) continue;

                rules.Add(new JoinRule
                {
                    Order = rules.Count + 1,
                    CategoryA = defaultRule.CategoryA,
                    CategoryB = defaultRule.CategoryB,
                    IsEnabled = defaultRule.IsEnabled
                });

                changed = true;
            }

            for (int i = 0; i < rules.Count; i++)
                rules[i].Order = i + 1;

            if (changed)
                Save(rules);

            return rules;
        }

        private static readonly string DmuStatePath =
            Path.Combine(ConfigDir, "dmu_enabled.txt");

        public static bool LoadDmuEnabled()
        {
            try
            {
                if (File.Exists(DmuStatePath))
                    return File.ReadAllText(DmuStatePath).Trim() == "1";
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"[AutoJoin Config] LoadDmuEnabled error: {ex.Message}");
            }
            return false;
        }

        public static void SaveDmuEnabled(bool enabled)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                File.WriteAllText(DmuStatePath, enabled ? "1" : "0");
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"[AutoJoin Config] SaveDmuEnabled error: {ex.Message}");
            }
        }

        // ── Mini JSON Serializer (cho List<JoinRule>) ────────────────────

        private static string SerializeRules(List<JoinRule> list)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                sb.AppendLine("  {");
                sb.AppendLine($"    \"Order\": {r.Order},");
                sb.AppendLine($"    \"CategoryA\": \"{Esc(r.CategoryA)}\",");
                sb.AppendLine($"    \"CategoryB\": \"{Esc(r.CategoryB)}\",");
                sb.AppendLine($"    \"IsEnabled\": {(r.IsEnabled ? "true" : "false")}");
                sb.Append(i < list.Count - 1 ? "  }," : "  }");
                sb.AppendLine();
            }
            sb.AppendLine("]");
            return sb.ToString();
        }

        private static List<JoinRule> DeserializeRules(string json)
        {
            var result = new List<JoinRule>();

            var objectMatches = Regex.Matches(json, @"\{([^}]+)\}", RegexOptions.Singleline);
            foreach (Match m in objectMatches)
            {
                var body = m.Groups[1].Value;
                var item = new JoinRule();

                var orderMatch = Regex.Match(body, @"""Order""\s*:\s*(\d+)");
                var catAMatch  = Regex.Match(body, @"""CategoryA""\s*:\s*""([^""]+)""");
                var catBMatch  = Regex.Match(body, @"""CategoryB""\s*:\s*""([^""]+)""");
                var enMatch    = Regex.Match(body, @"""IsEnabled""\s*:\s*(true|false)");

                if (orderMatch.Success) item.Order        = int.Parse(orderMatch.Groups[1].Value);
                if (catAMatch.Success)  item.CategoryA    = catAMatch.Groups[1].Value;
                if (catBMatch.Success)  item.CategoryB    = catBMatch.Groups[1].Value;
                if (enMatch.Success)    item.IsEnabled    = enMatch.Groups[1].Value == "true";

                if (!string.IsNullOrEmpty(item.CategoryA) && !string.IsNullOrEmpty(item.CategoryB))
                    result.Add(item);
            }

            return result.Count > 0 ? result : null;
        }

        private static string Esc(string s)
        {
            if (s == null) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
