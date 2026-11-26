// Pure code (no Revit API): compiled into tests/Addin.Tests by path.
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Addin
{
    /// <summary>
    /// Per-PC switches, in %AppData%\&lt;product&gt;\addin.json. The snapshot after Synchronize with Central is ON unless
    /// that file says <c>"snapshotAfterSync": false</c> (engineer, 2026-11-26: "Let us do the automatic snapshot after
    /// every synchronize with central. Those added seconds are fine"; the first real read took 3.7 to 10.9 s for a
    /// 6,000-element tower). A missing or unreadable file means on. No JSON library: the add-in carries no
    /// third-party files.
    /// </summary>
    public static class AddinSettings
    {
        private static readonly Regex SNAPSHOT_AFTER_SYNC_OFF = new Regex("\"snapshotAfterSync\"\\s*:\\s*false", RegexOptions.IgnoreCase);

        public static string PathFor(string productName)
        {
            string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(roamingAppData, productName, "addin.json");
        }

        public static bool IsSnapshotAfterSyncOn(string? settingsText)
        {
            return settingsText == null || !SNAPSHOT_AFTER_SYNC_OFF.IsMatch(settingsText);
        }

        public static bool IsSnapshotAfterSyncOn(string productName, Func<string, string?> readFile)
        {
            try
            {
                return IsSnapshotAfterSyncOn(readFile(PathFor(productName)));
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
