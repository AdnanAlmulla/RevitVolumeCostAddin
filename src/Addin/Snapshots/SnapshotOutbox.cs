// Pure code (no Revit API): compiled into tests/Addin.Tests by path.
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Contracts;

namespace Addin.Snapshots
{
    /// <summary>
    /// The outbox: a folder on this PC where the add-in leaves each snapshot for the uploader to send (CLAUDE.md "The
    /// add-in writes, the uploader sends"). It is outside OneDrive, in %LocalAppData%\&lt;product&gt;\Outbox. A snapshot
    /// is written under a temporary name and renamed when complete, so the uploader never picks up half a file.
    /// </summary>
    public static class SnapshotOutbox
    {
        public const string SNAPSHOT_FILE_EXTENSION = ".snapshot.json.gz";
        private const string PARTIAL_FILE_EXTENSION = ".partial";

        public static string FolderFor(string productName)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, productName, "Outbox");
        }

        public static string Write(Snapshot snapshot, string outboxFolder)
        {
            Directory.CreateDirectory(outboxFolder);
            string timeStamp = snapshot.TakenAtUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string fileName = timeStamp + "_" + snapshot.SnapshotId.ToString("N") + SNAPSHOT_FILE_EXTENSION;
            string finalPath = Path.Combine(outboxFolder, fileName);
            string partialPath = finalPath + PARTIAL_FILE_EXTENSION;

            using (FileStream file = File.Create(partialPath))
            using (var compressed = new GZipStream(file, CompressionLevel.Optimal))
            using (var text = new StreamWriter(compressed, new UTF8Encoding(false)))
            {
                SnapshotJson.Write(snapshot, text);
            }
            File.Move(partialPath, finalPath);
            return finalPath;
        }
    }
}
