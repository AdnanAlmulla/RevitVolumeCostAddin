using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Addin.Commands;
using Addin.Ribbon;
using Addin.Snapshots;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Contracts;

namespace Addin
{
    /// <summary>
    /// Starts with Revit: takes a snapshot after every successful Synchronize with Central, with its 3D shapes (not on
    /// Save: CLAUDE.md "It pushes snapshots"), unless this PC switches it off (AddinSettings), and adds the ribbon tab
    /// (Sync to website, Raw dump, Help). That snapshot can't fail the sync: it runs after Revit has finished, only reads,
    /// shows nothing, and a failure is only logged. It holds Revit while it reads: 3.7 to 10.9 s on a 6,000-element
    /// tower, plus about 1 s for the shapes, which the engineer accepted (2026-11-26).
    /// </summary>
    public sealed class App : IExternalApplication
    {
        // Each ribbon button and the icon it shows, so the icons can follow a change of Revit's theme.
        private static readonly List<KeyValuePair<PushButton, string>> ICON_BY_BUTTON = new List<KeyValuePair<PushButton, string>>();

        public Result OnStartup(UIControlledApplication application)
        {
            // The snapshot first: nothing on the ribbon may cost it.
            application.ControlledApplication.DocumentSynchronizedWithCentral += OnSynchronizedWithCentral;
            try
            {
                AddRibbon(application);
            }
            catch (Exception exception)
            {
                // Such as a second copy of the add-in having made the tab already. The snapshot after sync still works.
                AddinInfo.Log("Ribbon not added, " + exception.GetType().Name + ": " + exception.Message);
            }
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

            RibbonPanel quantitiesPanel = application.CreateRibbonPanel(tabName, "Quantities");
            AddButton(quantitiesPanel, "sync", new PushButtonData("SyncToWebsite", "Sync to\nwebsite", AddinInfo.AssemblyPath, typeof(SyncCommand).FullName)
            {
                ToolTip = "Takes a snapshot of this model's quantities now and leaves it for the uploader to send. Changes nothing in the model.",
            });
            // Raw dump is for checking only, so it waits in the panel's drop-down (the arrow beside "Quantities"), out of
            // sight until asked for (engineer, 2026-11-27).
            quantitiesPanel.AddSlideOut();
            AddButton(quantitiesPanel, "rawdump",new PushButtonData("RawDump", "Raw\ndump", AddinInfo.AssemblyPath, typeof(RawDumpCommand).FullName)
            {
                ToolTip = "Writes every element with all its parameters to one file, for checking. Changes nothing in the model.",
            });

            RibbonPanel helpPanel = application.CreateRibbonPanel(tabName, "Help");
            var helpButtonData = new PushButtonData("Help", "Help", AddinInfo.AssemblyPath, typeof(HelpCommand).FullName)
            {
                ToolTip = "What " + tabName + " does, whether snapshots are waiting to be sent, and where its files are.",
            };
            // Help works with no model open.
            helpButtonData.AvailabilityClassName = typeof(AlwaysAvailable).FullName;
            AddButton(helpPanel, "help", helpButtonData);

            ShowIconsForTheme();
            FollowThemeChanges(application);
        }

        private static void AddButton(RibbonPanel panel, string iconName, PushButtonData buttonData)
        {
            if (panel.AddItem(buttonData) is PushButton button)
            {
                ICON_BY_BUTTON.Add(new KeyValuePair<PushButton, string>(button, iconName));
            }
        }

        /// <summary>Icons drawn for the light or dark theme. A missing icon leaves the button with its label only.</summary>
        private static void ShowIconsForTheme()
        {
            bool isDarkTheme = IsDarkTheme();
            foreach (KeyValuePair<PushButton, string> buttonAndIcon in ICON_BY_BUTTON)
            {
                try
                {
                    buttonAndIcon.Key.LargeImage = RibbonIcons.Load(buttonAndIcon.Value, isDarkTheme, RibbonIcons.LARGE_ICON_SIZE_PIXELS);
                    buttonAndIcon.Key.Image = RibbonIcons.Load(buttonAndIcon.Value, isDarkTheme, RibbonIcons.SMALL_ICON_SIZE_PIXELS);
                }
                catch (Exception exception)
                {
                    AddinInfo.Log("Ribbon icon " + buttonAndIcon.Value + " not shown, " + exception.GetType().Name + ": " + exception.Message);
                }
            }
        }

        // The theme calls sit in their own methods, kept out of their callers, so a Revit build without them fails here,
        // inside a try, and not when the caller is compiled. Revit 2024 added the dark theme and this API.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsDarkTheme()
        {
            try
            {
                return ReadIsDarkTheme();
            }
            catch (Exception)
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadIsDarkTheme()
        {
            return UIThemeManager.CurrentTheme == UITheme.Dark;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FollowThemeChanges(UIControlledApplication application)
        {
            try
            {
                SubscribeToThemeChanges(application);
            }
            catch (Exception exception)
            {
                AddinInfo.Log("Ribbon icons will not follow a theme change, " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SubscribeToThemeChanges(UIControlledApplication application)
        {
            application.ThemeChanged += OnThemeChanged;
        }

        private static void OnThemeChanged(object? sender, ThemeChangedEventArgs arguments)
        {
            ShowIconsForTheme();
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
                string shapesNote = SyncCommand.WriteShapes(document, snapshot, snapshotPath);
                totalTime.Stop();
                AddinInfo.Log($"After sync: {snapshot.Elements.Count} elements ({snapshot.UnreadableElementCount} unreadable), read {snapshot.ReadSeconds:0.0} s, {shapesNote}, total {totalTime.Elapsed.TotalSeconds:0.0} s, {snapshotPath}");
            }
            catch (Exception exception)
            {
                AddinInfo.Log("After sync: no snapshot, " + exception.GetType().Name + ": " + exception.Message);
            }
        }
    }

    /// <summary>Lets a button work with no model open (Revit greys out external commands otherwise).</summary>
    public sealed class AlwaysAvailable : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return true;
        }
    }
}
