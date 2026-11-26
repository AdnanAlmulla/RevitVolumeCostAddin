using System.Diagnostics;
using System.Globalization;
using System;
using Addin.Shapes;
using Addin.Snapshots;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Contracts;

namespace Addin.Commands
{
    /// <summary>
    /// The "Sync to website" button: takes a snapshot of the open model now, with its 3D shapes, and leaves both in the
    /// outbox for the uploader. Works on any project model, a detached copy included (the automatic one after Synchronize with
    /// Central skips those). Read-only: Revit itself stops it changing the model.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class SyncCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document? document = commandData.Application.ActiveUIDocument?.Document;
            if (document == null || document.IsFamilyDocument)
            {
                message = "Open the project model first, then press Sync to website again.";
                return Result.Failed;
            }

            Stopwatch totalTime = Stopwatch.StartNew();
            Snapshot snapshot = SnapshotReader.Read(document, commandData.Application.Application, SnapshotTriggers.MANUAL_SYNC, AddinInfo.Version);
            string outboxFolder = SnapshotOutbox.FolderFor(AddinInfo.ProductName);
            string snapshotPath = SnapshotOutbox.Write(snapshot, outboxFolder);
            string shapesNote = WriteShapes(document, snapshot, snapshotPath);
            totalTime.Stop();
            AddinInfo.Log($"Manual sync: {snapshot.Elements.Count} elements ({snapshot.UnreadableElementCount} unreadable), read {snapshot.ReadSeconds:0.0} s, {shapesNote}, total {totalTime.Elapsed.TotalSeconds:0.0} s, {snapshotPath}");

            string seconds = totalTime.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture);
            var dialog = new TaskDialog("Sync to website")
            {
                MainInstruction = "Snapshot taken",
                MainContent =
                    snapshot.Elements.Count + " elements in " + seconds + " seconds" +
                    (snapshot.UnreadableElementCount > 0 ? "; " + snapshot.UnreadableElementCount + " could not be read" : "") + ".\n\n" +
                    "The uploader sends it to the website. Its file:\n" + snapshotPath,
                FooterText = "Nothing in the model was changed.",
            };
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Show the file in File Explorer");
            if (dialog.Show() == TaskDialogResult.CommandLink1)
            {
                Process.Start("explorer.exe", "/select,\"" + snapshotPath + "\"");
            }
            return Result.Succeeded;
        }

        /// <summary>The shapes beside the snapshot; a failure costs the 3D view an update, never the snapshot.</summary>
        internal static string WriteShapes(Document document, Snapshot snapshot, string snapshotPath)
        {
            try
            {
                ShapePassOutcome shapes = ShapeOutbox.Write(document, snapshot, snapshotPath);
                ShapeOutbox.LogFailures(shapes);
                return ShapeOutbox.Describe(shapes);
            }
            catch (Exception exception)
            {
                return "no 3D shapes (" + exception.GetType().Name + ": " + exception.Message + ")";
            }
        }
    }
}
