using System;
using System.Collections.Generic;
using System.IO;

namespace LOQN1_Location_element
{
    /// <summary>
    /// Lưu và đọc bảng mapping tên Level từ file text.
    /// File lưu tại: %APPDATA%\VILAIVIET\LOQN1_Location\level_mapping.txt
    /// Format mỗi dòng: TenGoc=TenHienThi
    /// </summary>
    public static class LevelMappingStorage
    {
        private static string GetStoragePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "VILAIVIET", "LOQN1_Location");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return Path.Combine(folder, "level_mapping.txt");
        }

        /// <summary>
        /// Lưu bảng mapping ra file.
        /// </summary>
        public static void Save(Dictionary<string, string> mapping)
        {
            try
            {
                string path = GetStoragePath();
                List<string> lines = new List<string>();

                foreach (var kvp in mapping)
                {
                    // Chỉ lưu nếu tên hiển thị khác tên gốc
                    if (kvp.Key != kvp.Value)
                    {
                        lines.Add(kvp.Key + "=" + kvp.Value);
                    }
                }

                File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
            }
            catch (Exception)
            {
                // Không lưu được thì bỏ qua (không crash tool)
            }
        }

        /// <summary>
        /// Đọc bảng mapping từ file (nếu có).
        /// </summary>
        public static Dictionary<string, string> Load()
        {
            Dictionary<string, string> mapping = new Dictionary<string, string>();

            try
            {
                string path = GetStoragePath();

                if (!File.Exists(path))
                {
                    return mapping;
                }

                string[] lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    int eqIndex = line.IndexOf('=');
                    if (eqIndex > 0 && eqIndex < line.Length - 1)
                    {
                        string key = line.Substring(0, eqIndex).Trim();
                        string value = line.Substring(eqIndex + 1).Trim();

                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        {
                            mapping[key] = value;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Không đọc được thì trả về rỗng
            }

            return mapping;
        }
    }
}
