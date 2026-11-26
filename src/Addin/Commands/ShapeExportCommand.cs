using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Addin.Json;
using Addin.Shapes;
using Addin.Snapshots;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Contracts;

namespace Addin.Commands
{
    /// <summary>
    /// "3D shapes" button: the timing probe for the website's 3D view (backlog F-003). Reads the shape of every element a
    /// snapshot reads, twice: once at the setting the website would use, written to Documents\&lt;product&gt;\Shapes\, and
    /// once finer, counted only, so one run shows what more detail would cost. Times each part and logs it to addin.log.
    /// Not part of a sync: whether shapes can ride on one is the engineer's call once these times are known.
    /// ReadOnly: Revit itself stops this command from changing the model.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class ShapeExportCommand : IExternalCommand
    {
        private const string SHAPES_FOLDER_NAME = "Shapes";
        private const string PARTIAL_FILE_EXTENSION = ".partial";
        private const double BYTES_PER_MEGABYTE = 1024.0 * 1024.0;

        // The setting written to the file: Medium detail and the coarsest triangles, the lightest for a browser.
        private static readonly ShapeSetting WRITTEN_SETTING = new ShapeSetting("medium-coarse", ViewDetailLevel.Medium, 0.0);
        // Counted only, to show what finer shapes would cost (round piles and columns gain the most triangles).
        private static readonly ShapeSetting COUNTED_SETTING = new ShapeSetting("fine-half", ViewDetailLevel.Fine, 0.5);

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document? document = commandData.Application.ActiveUIDocument?.Document;
            if (document == null)
            {
                message = "Open the project model first, then run 3D shapes again.";
                return Result.Failed;
            }
            if (document.IsFamilyDocument)
            {
                message = "3D shapes reads a project model, not a family. Open the project model and run it again.";
                return Result.Failed;
            }

            Stopwatch selectTime = Stopwatch.StartNew();
            List<Element> shapedElements = ElementsWithVolume(document);
            selectTime.Stop();
            AddinInfo.Log($"3D shapes: started on {document.Title}, {shapedElements.Count} elements with a volume, found in {selectTime.Elapsed.TotalSeconds:0.0} s");

            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string shapesFolder = Path.Combine(documentsFolder, AddinInfo.ProductName, SHAPES_FOLDER_NAME);
            Directory.CreateDirectory(shapesFolder);
            string timeStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string filePath = Path.Combine(shapesFolder, SafeFileName(document.Title) + "_" + timeStamp + ShapeFile.FILE_EXTENSION);
            string partialPath = filePath + PARTIAL_FILE_EXTENSION;

            ShapePassOutcome writtenPass;
            ShapePassOutcome countedPass;
            using (FileStream file = File.Create(partialPath))
            using (var compressed = new GZipStream(file, CompressionLevel.Optimal))
            using (var text = new StreamWriter(compressed, new UTF8Encoding(false)))
            {
                var json = new JsonWriter(text);
                json.BeginObject();
                WriteHeader(json, document, commandData.Application.Application);
                json.PropertyName("elements");
                json.BeginArray();
                writtenPass = ShapeReader.Run(shapedElements, WRITTEN_SETTING, json);
                json.EndArray();
                ShapeReader.Log(writtenPass);

                countedPass = ShapeReader.Run(shapedElements, COUNTED_SETTING, null);
                ShapeReader.Log(countedPass);

                json.PropertyName("summary");
                json.BeginObject();
                json.Property("selectSeconds", selectTime.Elapsed.TotalSeconds);
                json.PropertyName("passes");
                json.BeginArray();
                ShapeReader.WriteSummary(json, writtenPass);
                ShapeReader.WriteSummary(json, countedPass);
                json.EndArray();
                json.EndObject();
                json.EndObject();
            }
            File.Move(partialPath, filePath);
            double fileSizeMb = new FileInfo(filePath).Length / BYTES_PER_MEGABYTE;
            AddinInfo.Log($"3D shapes: finished, {fileSizeMb:0.0} MB, {filePath}");

            ShowFinished(writtenPass, countedPass, fileSizeMb, filePath);
            return Result.Succeeded;
        }

        /// <summary>The same elements a snapshot holds: model elements with a material volume.</summary>
        private static List<Element> ElementsWithVolume(Document document)
        {
            var shapedElements = new List<Element>();
            foreach (Element element in SnapshotReader.ModelElements(document))
            {
                try
                {
                    if (SnapshotReader.HasMaterialVolume(element))
                    {
                        shapedElements.Add(element);
                    }
                }
                catch (Exception)
                {
                    // The snapshot counts such an element as unreadable; the shape pass has nothing to draw for it.
                }
            }
            return shapedElements;
        }

        private static void WriteHeader(JsonWriter json, Document document, Autodesk.Revit.ApplicationServices.Application application)
        {
            ModelIdentity model = SnapshotReader.ModelIdentityOf(document);
            json.Property("format", ShapeFile.FORMAT);
            json.Property("takenAtUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            json.Property("addinVersion", AddinInfo.Version);
            json.Property("revitVersion", application.SubVersionNumber);
            json.Property("revitBuild", application.VersionBuild);
            json.PropertyName("model");
            json.BeginObject();
            json.Property("title", model.Title);
            json.Property("isInCloud", model.IsInCloud);
            json.Property("accProjectId", model.AccProjectId?.ToString());
            json.Property("accModelId", model.AccModelId?.ToString());
            json.EndObject();
            json.Property("axes", ShapeFile.AXES);
            json.Property("setting", WRITTEN_SETTING.Name);
        }

        private static void ShowFinished(ShapePassOutcome writtenPass, ShapePassOutcome countedPass, double fileSizeMb, string filePath)
        {
            string content = string.Format(CultureInfo.CurrentCulture,
                "{0:N0} elements read in {1:0} seconds ({2:N0} triangles, {3:0.0} MB).\n" +
                "Finer shapes, counted only: {4:0} seconds ({5:N0} triangles).\n\n" +
                "Send back this file and addin.log (%LocalAppData%\\{6}\\Logs):\n{7}",
                writtenPass.ElementCount, writtenPass.TotalSeconds, writtenPass.TriangleCount, fileSizeMb,
                countedPass.TotalSeconds, countedPass.TriangleCount, AddinInfo.ProductName, filePath);
            var dialog = new TaskDialog("3D shapes finished")
            {
                MainInstruction = "3D shapes finished",
                MainContent = content,
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
            return new string(title.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
        }
    }
}
