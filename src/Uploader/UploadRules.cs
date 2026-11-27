// Pure code: compiled into tests/Addin.Tests by path.
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
