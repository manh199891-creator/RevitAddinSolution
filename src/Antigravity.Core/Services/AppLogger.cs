using System;
using System.IO;
using Serilog;

namespace Antigravity.Core.Services
{
    public static class AppLogger
    {
        private static readonly object SyncRoot = new object();
        private static bool _configured;

        public static void Information(string message)
        {
            EnsureConfigured();
            Log.Information(message);
        }

        public static void Warning(string message)
        {
            EnsureConfigured();
            Log.Warning(message);
        }

        public static void Error(string message)
        {
            EnsureConfigured();
            Log.Error(message);
        }

        public static void Error(Exception exception, string message)
        {
            EnsureConfigured();
            Log.Error(exception, message);
        }

        private static void EnsureConfigured()
        {
            if (_configured) return;

            lock (SyncRoot)
            {
                if (_configured) return;

                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Antigravity",
                    "Logs");

                Directory.CreateDirectory(logDir);

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        Path.Combine(logDir, "antigravity-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14,
                        shared: true)
                    .CreateLogger();

                _configured = true;
            }
        }
    }
}
