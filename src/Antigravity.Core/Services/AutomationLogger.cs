using System;
using System.IO;

namespace Antigravity.Core.Services
{
    public static class AutomationLogger
    {
        // Thư mục lưu log
        private static readonly string LogDir = @"C:\temp\AntigravityLogs";

        public static void Write(string context, string message)
        {
            try
            {
                if (!Directory.Exists(LogDir))
                {
                    Directory.CreateDirectory(LogDir);
                }

                string timeStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                string fileName = $"{timeStamp}-{context}.log";
                string filePath = Path.Combine(LogDir, fileName);

                string content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{context}] {message}{Environment.NewLine}";
                
                File.WriteAllText(filePath, content);
            }
            catch
            {
                // Bỏ qua lỗi log để không làm gián đoạn Revit
            }
        }

        public static void WriteError(string context, Exception ex)
        {
            Write(context, $"ERROR: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }
    }
}
