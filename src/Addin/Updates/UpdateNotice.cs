using System;
using System.Diagnostics;
using System.IO;
using Autodesk.Revit.UI;

namespace Addin.Updates
{
    /// <summary>
    /// Tells the technician, once per Revit session, that a newer version is installed and starts at the next Revit start
    /// (engineer, 2026-11-27: "new version of Telltale Addin ready, restart Revit please"). The uploader installs updates
    /// by pointing this Revit year's manifest at a new version folder; this compares the manifest with the running
    /// version. Checked while Revit is idle, at most every few minutes; it never touches the model.
    /// </summary>
    internal static class UpdateNotice
    {
        private static readonly TimeSpan TIME_BETWEEN_CHECKS = TimeSpan.FromMinutes(5);
        private static readonly Stopwatch TIME_SINCE_LAST_CHECK = new Stopwatch();
        private static bool isShownThisSession;

        /// <summary>
        /// This Revit year's manifest: the DLL is at Addins\&lt;year&gt;\&lt;product&gt;\&lt;version&gt;\&lt;product&gt;.dll, and the
        /// manifest at Addins\&lt;year&gt;\&lt;product&gt;.addin.
        /// </summary>
        private static string ManifestPath()
        {
            string versionFolder = Path.GetDirectoryName(AddinInfo.AssemblyPath) ?? "";
            string productFolder = Path.GetDirectoryName(versionFolder) ?? "";
            string yearFolder = Path.GetDirectoryName(productFolder) ?? "";
            return Path.Combine(yearFolder, AddinInfo.ProductName + ".addin");
        }

        /// <summary>Called on Revit's Idling event. Never throws: a failed check waits for the next one.</summary>
        public static void CheckWhenDue()
        {
            if (isShownThisSession || (TIME_SINCE_LAST_CHECK.IsRunning && TIME_SINCE_LAST_CHECK.Elapsed < TIME_BETWEEN_CHECKS))
            {
                return;
            }
            TIME_SINCE_LAST_CHECK.Restart();
            try
            {
                string manifestPath = ManifestPath();
                if (!File.Exists(manifestPath))
                {
                    return;
                }
                Version? installedVersion = ReleaseVersions.FromManifest(File.ReadAllText(manifestPath));
                Version? runningVersion = ReleaseVersions.Parse(AddinInfo.Version);
                if (!ReleaseVersions.IsNewer(installedVersion, runningVersion))
                {
                    return;
                }
                isShownThisSession = true;
                AddinInfo.Log("Update: " + installedVersion + " installed, " + runningVersion + " running; told the technician to restart Revit");
                var dialog = new TaskDialog(AddinInfo.ProductName)
                {
                    MainInstruction = "New version of " + AddinInfo.ProductName + " Addin ready, restart Revit please",
                    MainContent = "Version " + installedVersion + " is installed and starts the next time Revit starts. This session runs " + runningVersion + ".",
                };
                dialog.Show();
            }
            catch (Exception exception)
            {
                AddinInfo.Log("Update check failed, " + exception.GetType().Name + ": " + exception.Message);
            }
        }
    }
}
