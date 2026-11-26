using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Addin.Snapshots;
using Contracts;
using Uploader;
using Xunit;

namespace Addin.Tests
{
    public class OutboxAndUploadTests
    {
        [Fact]
        public void A_snapshot_in_the_outbox_is_complete_and_has_no_partial_file_left()
        {
            string outboxFolder = Path.Combine(Path.GetTempPath(), "outbox-test-" + Guid.NewGuid().ToString("N"));
            var snapshot = new Snapshot { SnapshotId = Guid.NewGuid(), TakenAtUtc = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc), Model = new ModelIdentity { Title = "Tower" } };
            try
            {
                string snapshotPath = SnapshotOutbox.Write(snapshot, outboxFolder);

                Assert.EndsWith(SnapshotOutbox.SNAPSHOT_FILE_EXTENSION, snapshotPath);
                Assert.Single(Directory.GetFiles(outboxFolder));
                using var unzipped = new StreamReader(new GZipStream(File.OpenRead(snapshotPath), CompressionMode.Decompress));
                Snapshot read = JsonSerializer.Deserialize<Snapshot>(unzipped.ReadToEnd(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
                Assert.Equal(snapshot.SnapshotId, read.SnapshotId);
            }
            finally
            {
                Directory.Delete(outboxFolder, recursive: true);
            }
        }

        [Theory]
        [InlineData(HttpStatusCode.OK, UploadOutcome.Delivered)]
        [InlineData(HttpStatusCode.Created, UploadOutcome.Delivered)]
        [InlineData(HttpStatusCode.BadRequest, UploadOutcome.Rejected)]
        [InlineData(HttpStatusCode.UnsupportedMediaType, UploadOutcome.Rejected)]
        [InlineData(HttpStatusCode.Unauthorized, UploadOutcome.TryAgain)]
        [InlineData(HttpStatusCode.Forbidden, UploadOutcome.TryAgain)]
        [InlineData(HttpStatusCode.InternalServerError, UploadOutcome.TryAgain)]
        [InlineData(HttpStatusCode.ServiceUnavailable, UploadOutcome.TryAgain)]
        [InlineData(HttpStatusCode.RequestTimeout, UploadOutcome.TryAgain)]
        [InlineData(HttpStatusCode.TooManyRequests, UploadOutcome.TryAgain)]
        public void Each_answer_from_the_website_has_one_outcome(HttpStatusCode statusCode, UploadOutcome expected)
        {
            Assert.Equal(expected, UploadRules.OutcomeOf(statusCode));
        }

        [Fact]
        public void No_answer_at_all_is_tried_again()
        {
            Assert.Equal(UploadOutcome.TryAgain, UploadRules.OutcomeOf(null));
        }
    }
}
