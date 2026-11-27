using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Addin.Snapshots;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Addin.Commands
{
    /// <summary>
    /// The "Help" button: what the add-in does, whether this PC takes the snapshot after each sync, how many snapshots are
    /// still waiting for the uploader, and links to the website, the instruction sheet, the outbox and the logs. Reads
    /// files only; it never touches the model, so it works with no model open.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class HelpCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string productName = AddinInfo.ProductName;
            string outboxFolder = SnapshotOutbox.FolderFor(productName);
            string logFolder = AddinInfo.LogFolder;
            string? siteAddress = UploaderSettingsFile.SiteAddressFrom(ReadTextOrNull(UploaderSettingsFile.PathFor(productName)));
            bool isInstructionSheetInstalled = File.Exists(AddinInfo.InstructionSheetPath);
            bool isSnapshotAfterSyncOn = AddinSettings.IsSnapshotAfterSyncOn(productName, ReadTextOrNull);
            OutboxWaiting waiting = SnapshotOutbox.Waiting(outboxFolder);

            var dialog = new TaskDialog(productName + " help")
            {
                // The release number alone, without the build's commit suffix ("0.7.0", not "0.7.0+4c151c4...").
                MainInstruction = productName + " " + (Updates.ReleaseVersions.Parse(AddinInfo.Version)?.ToString() ?? AddinInfo.Version),
                MainContent =
                    "Takes a snapshot of this model's quantities after each Synchronize with Central on an ACC model, and " +
                    "when you press Sync to website. The uploader sends each snapshot to the website. Nothing in the model is changed.\n\n" +
                    "Snapshot after Synchronize with Central: " + (isSnapshotAfterSyncOn ? "on" : "off on this PC") + ".\n" +
                    WaitingSentence(waiting),
            };
            if (siteAddress != null)
            {
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Open the website", siteAddress);
            }
            if (isInstructionSheetInstalled)
            {
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Open the instructions");
            }
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Show the outbox folder");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Show the logs folder");

            TaskDialogResult choice = dialog.Show();
            if (choice == TaskDialogResult.CommandLink1 && siteAddress != null)
            {
                OpenWithWindows(siteAddress);
            }
            else if (choice == TaskDialogResult.CommandLink2)
            {
                OpenWithWindows(AddinInfo.InstructionSheetPath);
            }
            else if (choice == TaskDialogResult.CommandLink3)
            {
                Directory.CreateDirectory(outboxFolder);
                OpenWithWindows(outboxFolder);
            }
            else if (choice == TaskDialogResult.CommandLink4)
            {
                Directory.CreateDirectory(logFolder);
                OpenWithWindows(logFolder);
            }
            return Result.Succeeded;
        }

        private static string WaitingSentence(OutboxWaiting waiting)
        {
            if (waiting.SnapshotCount == 0 || waiting.OldestWrittenAtLocal == null)
            {
                return "Waiting to be sent: none.";
            }
            string oldestText = waiting.OldestWrittenAtLocal.Value.ToString("d MMM HH:mm", CultureInfo.CurrentCulture);
            string countText = waiting.SnapshotCount == 1 ? "1 snapshot" : waiting.SnapshotCount + " snapshots";
            return "Waiting to be sent: " + countText + ", the oldest from " + oldestText + ". " +
                "If they stay, the uploader is not reaching the website: see uploader.log in the logs folder.";
        }

        private static string? ReadTextOrNull(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>Opens a folder in File Explorer, a .txt file in its usual program and a web address in the browser.</summary>
        private static void OpenWithWindows(string pathOrAddress)
        {
            try
            {
                Process.Start(new ProcessStartInfo(pathOrAddress) { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                AddinInfo.Log("Help: could not open " + pathOrAddress + ", " + exception.GetType().Name + ": " + exception.Message);
                TaskDialog.Show(AddinInfo.ProductName + " help", "Windows could not open:\n" + pathOrAddress);
            }
        }
    }
}
