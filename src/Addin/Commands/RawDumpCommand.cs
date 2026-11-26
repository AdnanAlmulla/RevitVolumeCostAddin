using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Addin.RawDump;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Addin.Commands
{
    /// <summary>
    /// Add-Ins tab > External Tools > "&lt;product&gt;: raw dump". Writes the open model to one file in
    /// Documents\&lt;product&gt;\Raw dumps\ and shows where it is (backlog F-004).
    /// ReadOnly: Revit itself stops this command from changing the model.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class RawDumpCommand : IExternalCommand
    {
        private const string RAW_DUMP_FOLDER_NAME = "Raw dumps";
        private const string RAW_DUMP_FILE_EXTENSION = ".rawdump.json.gz";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document? document = commandData.Application.ActiveUIDocument?.Document;
            if (document == null)
            {
                message = "Open the project model first, then run the raw dump again.";
                return Result.Failed;
            }
            if (document.IsFamilyDocument)
            {
                message = "The raw dump reads a project model, not a family. Open the project model and run it again.";
                return Result.Failed;
            }

            // The product name comes from the assembly name, which Directory.Build.props sets.
            Assembly addinAssembly = typeof(RawDumpCommand).Assembly;
            string productName = addinAssembly.GetName().Name ?? "Addin";
            string addinVersion = addinAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? addinAssembly.GetName().Version?.ToString()
                ?? "unknown";

            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string dumpFolder = Path.Combine(documentsFolder, productName, RAW_DUMP_FOLDER_NAME);
            Directory.CreateDirectory(dumpFolder);
            string timeStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string fileName = SafeFileName(document.Title) + "_" + timeStamp + RAW_DUMP_FILE_EXTENSION;
            string filePath = Path.Combine(dumpFolder, fileName);

            var writer = new RawDumpWriter(document, commandData.Application.Application, addinVersion);
            RawDumpOutcome outcome = writer.WriteTo(filePath);

            ShowFinished(outcome, filePath);
            return Result.Succeeded;
        }

        private static void ShowFinished(RawDumpOutcome outcome, string filePath)
        {
            string elementCountText = outcome.ElementCount.ToString("N0", CultureInfo.CurrentCulture);
            string readTimeText = outcome.ReadTimeSeconds.ToString("0", CultureInfo.CurrentCulture);
            var dialog = new TaskDialog("Raw dump finished")
            {
                MainInstruction = "Raw dump finished",
                MainContent =
                    elementCountText + " elements written in " + readTimeText + " seconds.\n" +
                    outcome.UnreadableCount + " items could not be read; they are listed in the file.\n\n" +
                    "Send this file back as it is:\n" + filePath,
                FooterText = "Nothing in the model was changed.",
            };
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Show the file in File Explorer");
            TaskDialogResult choice = dialog.Show();
            if (choice == TaskDialogResult.CommandLink1)
            {
                Process.Start("explorer.exe", "/select,\"" + filePath + "\"");
            }
        }

        private static string SafeFileName(string title)
        {
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            string safeTitle = new string(title.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
            return safeTitle;
        }
    }
}
