// Pure code: compiled into tests/Addin.Tests by path.
using System;
using System.IO;
using System.Net;

namespace Uploader
{
    public enum UploadOutcome
    {
        /// <summary>The website has it (stored now, or already from an earlier try): move it to Sent.</summary>
        Delivered,
        /// <summary>The website refused this file and would refuse it again: move it to Rejected, with the reason.</summary>
        Rejected,
        /// <summary>The website could not be reached or was busy: leave it in the outbox and try again later.</summary>
        TryAgain,
    }

    public static class UploadRules
    {
        // The add-in writes "<name>.snapshot.json.gz" and its 3D shapes beside it as "<name>.shapes.json.gz"
        // (src/Addin/Snapshots/SnapshotOutbox.cs, src/Addin/Shapes/ShapeFile.cs).
        public const string SNAPSHOT_FILE_EXTENSION = ".snapshot.json.gz";
        public const string SHAPES_FILE_EXTENSION = ".shapes.json.gz";

        // How long Outbox\Sent keeps a file after it was sent (engineer, 2026-11-27: "90 days after it can be deleted").
        // The website keeps its own copy of every file it takes in, so this copy is a spare. Rejected is never emptied.
        public const int SENT_FILE_KEEPING_DAYS = 90;

        /// <summary>
        /// Deletes the files in Outbox\Sent sent more than SENT_FILE_KEEPING_DAYS before <paramref name="nowUtc"/>, and
        /// returns how many. A file's sending time is its last-write time, which the uploader sets as it moves the file
        /// into Sent (MarkAsSent). A file that can't be deleted now is left for a later pass.
        /// </summary>
        public static int DeleteExpiredSentFiles(string sentFolder, DateTime nowUtc)
        {
            if (!Directory.Exists(sentFolder))
            {
                return 0;
            }
            DateTime oldestKeptSentAtUtc = nowUtc.AddDays(-SENT_FILE_KEEPING_DAYS);
            int deletedCount = 0;
            foreach (string sentPath in Directory.GetFiles(sentFolder))
            {
                DateTime sentAtUtc = File.GetLastWriteTimeUtc(sentPath);
                if (sentAtUtc >= oldestKeptSentAtUtc)
                {
                    continue;
                }
                try
                {
                    File.Delete(sentPath);
                    deletedCount++;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            return deletedCount;
        }

        /// <summary>Stamps a file just moved into Sent with the time it was sent, which DeleteExpiredSentFiles counts from.</summary>
        public static void MarkAsSent(string sentPath, DateTime nowUtc)
        {
            File.SetLastWriteTimeUtc(sentPath, nowUtc);
        }

        /// <summary>
        /// The file name of the snapshot a shapes file belongs to. When the website refused that snapshot, it answers 409
        /// to its shapes for ever, so the shapes follow the snapshot to Rejected instead of being tried every minute.
        /// </summary>
        public static string SnapshotFileNameOf(string shapesFileName)
        {
            string name = shapesFileName.Substring(0, shapesFileName.Length - SHAPES_FILE_EXTENSION.Length);
            return name + SNAPSHOT_FILE_EXTENSION;
        }

        /// <summary>
        /// What one try at sending a snapshot means. No answer at all (the site is down, the PC is offline), a server
        /// error, a timeout or "too many requests" are worth retrying. So is a refused password (401, 403): it is this
        /// PC's sitePassword setting that is wrong or missing, not the file, so the file goes once the setting is fixed.
        /// So is "conflict" (409): 3D shapes whose model's snapshot the website has not taken in yet. Any other refusal
        /// is about the file itself.
        /// </summary>
        public static UploadOutcome OutcomeOf(HttpStatusCode? statusCode)
        {
            if (statusCode == null)
            {
                return UploadOutcome.TryAgain;
            }
            int code = (int)statusCode.Value;
            bool isSuccess = code >= 200 && code <= 299;
            if (isSuccess)
            {
                return UploadOutcome.Delivered;
            }
            bool isPasswordRefused = statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden;
            bool isModelNotYetKnown = statusCode == HttpStatusCode.Conflict;
            bool isWorthRetrying = code >= 500 || statusCode == HttpStatusCode.RequestTimeout || statusCode == HttpStatusCode.TooManyRequests || isPasswordRefused || isModelNotYetKnown;
            return isWorthRetrying ? UploadOutcome.TryAgain : UploadOutcome.Rejected;
        }
    }
}
