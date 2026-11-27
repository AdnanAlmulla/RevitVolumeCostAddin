using System;
using System.IO;
using Addin;
using Addin.Shapes;
using Addin.Snapshots;
using Contracts;
using Uploader;
using Xunit;

namespace Addin.Tests
{
    /// <summary>What the Help button reads (the website's address, the snapshots waiting) and how shapes follow their snapshot.</summary>
    public class HelpAndUploaderTests
    {
        [Theory]
        [InlineData("{ \"siteAddress\": \"http://host-pc:5080\", \"sitePassword\": \"x\" }", "http://host-pc:5080")]
        [InlineData("{\r\n  \"siteAddress\": \"https://site.example\"\r\n}", "https://site.example")]
        [InlineData("{ \"siteAddress\": \"\", \"sitePassword\": \"\" }", null)]
        [InlineData("{ \"siteAddress\": \"host-pc:5080\" }", null)]
        [InlineData("{ }", null)]
        [InlineData("not json at all", null)]
        [InlineData(null, null)]
        public void Only_a_web_address_is_offered_as_the_website(string? settingsText, string? expected)
        {
            Assert.Equal(expected, UploaderSettingsFile.SiteAddressFrom(settingsText));
        }

        [Fact]
        public void The_outbox_counts_only_snapshots_still_waiting()
        {
            string outboxFolder = Path.Combine(Path.GetTempPath(), "outbox-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var olderSnapshot = new Snapshot { SnapshotId = Guid.NewGuid(), TakenAtUtc = new DateTime(2026, 11, 27, 8, 0, 0, DateTimeKind.Utc), Model = new ModelIdentity() };
                var newerSnapshot = new Snapshot { SnapshotId = Guid.NewGuid(), TakenAtUtc = new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc), Model = new ModelIdentity() };
                string olderPath = SnapshotOutbox.Write(olderSnapshot, outboxFolder);
                SnapshotOutbox.Write(newerSnapshot, outboxFolder);
                var olderWrittenAtLocal = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Local);
                File.SetLastWriteTime(olderPath, olderWrittenAtLocal);
                // Neither a shapes file, a half-written file nor a sent snapshot is waiting.
                File.WriteAllText(Path.Combine(outboxFolder, "a" + ShapeFile.FILE_EXTENSION), "");
                File.WriteAllText(Path.Combine(outboxFolder, "b" + SnapshotOutbox.SNAPSHOT_FILE_EXTENSION + ".partial"), "");
                Directory.CreateDirectory(Path.Combine(outboxFolder, "Sent"));
                File.WriteAllText(Path.Combine(outboxFolder, "Sent", "c" + SnapshotOutbox.SNAPSHOT_FILE_EXTENSION), "");

                OutboxWaiting waiting = SnapshotOutbox.Waiting(outboxFolder);

                Assert.Equal(2, waiting.SnapshotCount);
                Assert.Equal(olderWrittenAtLocal, waiting.OldestWrittenAtLocal);
            }
            finally
            {
                Directory.Delete(outboxFolder, recursive: true);
            }
        }

        [Fact]
        public void An_outbox_that_does_not_exist_yet_has_nothing_waiting()
        {
            string missingFolder = Path.Combine(Path.GetTempPath(), "no-outbox-" + Guid.NewGuid().ToString("N"));

            OutboxWaiting waiting = SnapshotOutbox.Waiting(missingFolder);

            Assert.Equal(0, waiting.SnapshotCount);
            Assert.Null(waiting.OldestWrittenAtLocal);
        }

        [Fact]
        public void Shapes_name_the_snapshot_written_beside_them()
        {
            string shapesFileName = "20261127-080000_0123456789abcdef0123456789abcdef" + ShapeFile.FILE_EXTENSION;

            string snapshotFileName = UploadRules.SnapshotFileNameOf(shapesFileName);

            Assert.Equal("20261127-080000_0123456789abcdef0123456789abcdef" + SnapshotOutbox.SNAPSHOT_FILE_EXTENSION, snapshotFileName);
        }

        [Fact]
        public void Sent_files_are_deleted_only_after_90_days()
        {
            string sentFolder = Path.Combine(Path.GetTempPath(), "sent-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sentFolder);
            var nowUtc = new DateTime(2026, 11, 27, 6, 0, 0, DateTimeKind.Utc);
            string sent91DaysAgo = Path.Combine(sentFolder, "a" + SnapshotOutbox.SNAPSHOT_FILE_EXTENSION);
            string sent89DaysAgo = Path.Combine(sentFolder, "b" + SnapshotOutbox.SNAPSHOT_FILE_EXTENSION);
            string sentToday = Path.Combine(sentFolder, "c" + ShapeFile.FILE_EXTENSION);
            try
            {
                foreach (string path in new[] { sent91DaysAgo, sent89DaysAgo, sentToday })
                {
                    File.WriteAllText(path, "");
                }
                UploadRules.MarkAsSent(sent91DaysAgo, nowUtc.AddDays(-91));
                UploadRules.MarkAsSent(sent89DaysAgo, nowUtc.AddDays(-89));
                UploadRules.MarkAsSent(sentToday, nowUtc);

                int deletedCount = UploadRules.DeleteExpiredSentFiles(sentFolder, nowUtc);

                Assert.Equal(1, deletedCount);
                Assert.False(File.Exists(sent91DaysAgo));
                Assert.True(File.Exists(sent89DaysAgo));
                Assert.True(File.Exists(sentToday));
            }
            finally
            {
                Directory.Delete(sentFolder, recursive: true);
            }
        }

        [Fact]
        public void A_missing_sent_folder_deletes_nothing()
        {
            string missingFolder = Path.Combine(Path.GetTempPath(), "no-sent-" + Guid.NewGuid().ToString("N"));

            Assert.Equal(0, UploadRules.DeleteExpiredSentFiles(missingFolder, DateTime.UtcNow));
        }

        [Fact]
        public void The_uploader_and_the_add_in_agree_on_the_file_names()
        {
            Assert.Equal(SnapshotOutbox.SNAPSHOT_FILE_EXTENSION, UploadRules.SNAPSHOT_FILE_EXTENSION);
            Assert.Equal(ShapeFile.FILE_EXTENSION, UploadRules.SHAPES_FILE_EXTENSION);
        }
    }
}
