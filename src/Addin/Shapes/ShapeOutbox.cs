using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Addin.Json;
using Addin.Snapshots;
using Autodesk.Revit.DB;
using Contracts;

namespace Addin.Shapes
{
    /// <summary>
    /// Writes the 3D shapes of a snapshot's elements into the outbox beside it, for the website's 3D view (engineer,
    /// 2026-11-26: "Yes, the sync should send the shape. I wouldn't want them to click on the 3D button each time").
    /// Same name as the snapshot with ".shapes.json.gz", written under a temporary name and renamed when complete.
    /// Medium detail with the coarsest triangles (0.9 to 1.4 s and 1.2 MB on a 6,002-element tower, 2026-11-26); an
    /// element that fails is read again at Fine detail. Only reads, inside Revit's API context.
    /// </summary>
    internal static class ShapeOutbox
    {
        private const string PARTIAL_FILE_EXTENSION = ".partial";
        private static readonly ShapeSetting WRITTEN_SETTING = new ShapeSetting("medium-coarse", ViewDetailLevel.Medium, 0.0);
        // First run (2026-11-26): 159 of 6,002 elements failed at the written setting and none at this one.
        private static readonly ShapeSetting RETRY_SETTING = new ShapeSetting("fine-half", ViewDetailLevel.Fine, 0.5);

        public static ShapePassOutcome Write(Document document, Snapshot snapshot, string snapshotPath)
        {
            List<Element> elements = ElementsOf(document, snapshot);
            string shapesPath = snapshotPath.Substring(0, snapshotPath.Length - SnapshotOutbox.SNAPSHOT_FILE_EXTENSION.Length) + ShapeFile.FILE_EXTENSION;
            string partialPath = shapesPath + PARTIAL_FILE_EXTENSION;
            ShapePassOutcome outcome;
            using (FileStream file = File.Create(partialPath))
            using (var compressed = new GZipStream(file, CompressionLevel.Optimal))
            using (var text = new StreamWriter(compressed, new UTF8Encoding(false)))
            {
                var json = new JsonWriter(text);
                json.BeginObject();
                WriteHeader(json, snapshot);
                json.PropertyName("elements");
                json.BeginArray();
                outcome = ShapeReader.Run(elements, WRITTEN_SETTING, json, RETRY_SETTING);
                json.EndArray();
                json.PropertyName("summary");
                ShapeReader.WriteSummary(json, outcome);
                json.EndObject();
            }
            File.Move(partialPath, shapesPath);
            return outcome;
        }

        /// <summary>The snapshot's own elements, found again by UniqueId, so the shapes match its rows one for one.</summary>
        private static List<Element> ElementsOf(Document document, Snapshot snapshot)
        {
            var elements = new List<Element>();
            foreach (ElementFacts facts in snapshot.Elements)
            {
                Element? element = document.GetElement(facts.UniqueId);
                if (element != null)
                {
                    elements.Add(element);
                }
            }
            return elements;
        }

        private static void WriteHeader(JsonWriter json, Snapshot snapshot)
        {
            json.Property("format", ShapeFile.FORMAT);
            json.Property("snapshotId", snapshot.SnapshotId.ToString("D"));
            json.Property("takenAtUtc", snapshot.TakenAtUtc.ToString("o", CultureInfo.InvariantCulture));
            json.Property("addinVersion", snapshot.AddinVersion);
            json.Property("revitBuild", snapshot.RevitBuild);
            json.PropertyName("model");
            json.BeginObject();
            json.Property("title", snapshot.Model.Title);
            json.Property("isInCloud", snapshot.Model.IsInCloud);
            json.Property("accProjectId", snapshot.Model.AccProjectId?.ToString());
            json.Property("accModelId", snapshot.Model.AccModelId?.ToString());
            json.EndObject();
            json.Property("axes", ShapeFile.AXES);
            json.Property("setting", WRITTEN_SETTING.Name);
        }

        /// <summary>Why elements failed, one log line per reason, so a cause can be fixed.</summary>
        public static void LogFailures(ShapePassOutcome outcome)
        {
            foreach (KeyValuePair<string, int> failure in outcome.FailureCountByReason)
            {
                AddinInfo.Log($"3D shapes ({outcome.Setting.Name}): failed {failure.Value} times: {failure.Key}");
            }
        }

        public static string Describe(ShapePassOutcome outcome)
        {
            return string.Format(CultureInfo.InvariantCulture, "3D shapes {0:N0} triangles in {1:0.0} s ({2} read again finer, {3} unreadable)",
                outcome.TriangleCount, outcome.TotalSeconds, outcome.RetriedCount, outcome.UnreadableCount);
        }
    }
}
