// Pure code (no Revit API): compiled into tests/Addin.Tests by path.
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Addin
{
    /// <summary>
    /// Per-PC switches, in %AppData%\&lt;product&gt;\addin.json. The snapshot after Synchronize with Central is OFF until
    /// that file says <c>"snapshotAfterSync": true</c>: it holds Revit for the whole read after every sync of every
    /// cloud model, and its time is not measured yet (engineer's first snapshot test, 2026-11-26). A missing or
    /// unreadable file means off. No JSON library: the add-in carries no third-party files.
    /// </summary>
    public static class AddinSettings
    {
        private static readonly Regex SNAPSHOT_AFTER_SYNC_ON = new Regex("\"snapshotAfterSync\"\\s*:\\s*true", RegexOptions.IgnoreCase);

        public static string PathFor(string productName)
        {
            string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(roamingAppData, productName, "addin.json");
        }

        public static bool IsSnapshotAfterSyncOn(string? settingsText)
        {
            return settingsText != null && SNAPSHOT_AFTER_SYNC_ON.IsMatch(settingsText);
        }

        public static bool IsSnapshotAfterSyncOn(string productName, Func<string, string?> readFile)
        {
            try
            {
                return IsSnapshotAfterSyncOn(readFile(PathFor(productName)));
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
