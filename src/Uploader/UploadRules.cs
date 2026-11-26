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
        /// <summary>
        /// What one try at sending a snapshot means. No answer at all (the site is down, the PC is offline), a server
        /// error, a timeout or "too many requests" are worth retrying; any other refusal is about the file itself.
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
            bool isWorthRetrying = code >= 500 || statusCode == HttpStatusCode.RequestTimeout || statusCode == HttpStatusCode.TooManyRequests;
            return isWorthRetrying ? UploadOutcome.TryAgain : UploadOutcome.Rejected;
        }
    }
}
