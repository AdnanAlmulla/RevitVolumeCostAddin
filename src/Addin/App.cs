using System;
using System.Diagnostics;
using Addin.Commands;
using Addin.Snapshots;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Contracts;

namespace Addin
{
    /// <summary>
    /// Starts with Revit: adds the ribbon tab (Sync to website, Raw dump) and, when switched on for this PC
    /// (AddinSettings), takes a snapshot after every successful Synchronize with Central (not on Save: CLAUDE.md "It
    /// pushes snapshots"). That snapshot can't fail the sync: it runs after Revit has finished, only reads, shows
    /// nothing, and a failure is only logged. It does hold Revit while it reads, which is why it starts switched off.
    /// </summary>
    public sealed class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            AddRibbon(application);
            application.ControlledApplication.DocumentSynchronizedWithCentral += OnSynchronizedWithCentral;
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            application.ControlledApplication.DocumentSynchronizedWithCentral -= OnSynchronizedWithCentral;
            return Result.Succeeded;
        }

        private static void AddRibbon(UIControlledApplication application)
        {
            string tabName = AddinInfo.ProductName;
            application.CreateRibbonTab(tabName);
            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Quantities");
            panel.AddItem(new PushButtonData("SyncToWebsite", "Sync to\nwebsite", AddinInfo.AssemblyPath, typeof(SyncCommand).FullName)
            {
                ToolTip = "Takes a snapshot of this model's quantities now and leaves it for the uploader to send. Changes nothing in the model.",
            });
            panel.AddItem(new PushButtonData("RawDump", "Raw\ndump", AddinInfo.AssemblyPath, typeof(RawDumpCommand).FullName)
            {
                ToolTip = "Writes every element with all its parameters to one file, for checking. Changes nothing in the model.",
            });
        }

        /// <summary>
        /// Only a successful sync of a project model in the cloud is snapshotted. Family files, models not in ACC and
        /// detached copies are skipped (CLAUDE.md "It pushes snapshots"); the Sync button still works on those.
        /// </summary>
        private static void OnSynchronizedWithCentral(object? sender, DocumentSynchronizedWithCentralEventArgs arguments)
        {
            try
            {
                Document document = arguments.Document;
                bool isSwitchedOn = AddinSettings.IsSnapshotAfterSyncOn(AddinInfo.ProductName, path => System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null);
                bool isSnapshotted = isSwitchedOn
                    && arguments.Status == RevitAPIEventStatus.Succeeded
                    && !document.IsFamilyDocument
                    && !document.IsDetached
                    && document.IsModelInCloud;
                if (!isSnapshotted)
                {
                    return;
                }
                Stopwatch totalTime = Stopwatch.StartNew();
                Snapshot snapshot = SnapshotReader.Read(document, document.Application, SnapshotTriggers.SYNCHRONIZE_WITH_CENTRAL, AddinInfo.Version);
                string snapshotPath = SnapshotOutbox.Write(snapshot, SnapshotOutbox.FolderFor(AddinInfo.ProductName));
                totalTime.Stop();
                AddinInfo.Log($"After sync: {snapshot.Elements.Count} elements ({snapshot.UnreadableElementCount} unreadable), read {snapshot.ReadSeconds:0.0} s, total {totalTime.Elapsed.TotalSeconds:0.0} s, {snapshotPath}");
            }
            catch (Exception exception)
            {
                AddinInfo.Log("After sync: no snapshot, " + exception.GetType().Name + ": " + exception.Message);
            }
        }
    }
}
