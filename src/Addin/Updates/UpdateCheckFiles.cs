// Pure code (no Revit API): compiled into the uploader and into tests/Addin.Tests by path.
using System;

namespace Addin.Updates
{
    /// <summary>What the uploader found when asked to check for updates now.</summary>
    public enum UpdateCheckOutcome
    {
        /// <summary>Nothing newer is published than what is installed.</summary>
        UpToDate,
        /// <summary>A newer version was installed; it starts at the next Revit start.</summary>
        Installed,
        /// <summary>The website could not be reached (the host PC off, no network).</summary>
        Unreachable,
        /// <summary>The website refused this PC's site password.</summary>
        Refused,
        /// <summary>Anything else; the message says what.</summary>
        Failed,
    }

    /// <summary>
    /// How the Help button's "Check for updates" asks the uploader to check at once (the add-in never goes on the network
    /// itself): the add-in writes a request with a new id to %LocalAppData%\&lt;product&gt;\update-check.request; the
    /// uploader, which watches for it, checks and writes update-check.result: the request's id, the outcome, and a sentence
    /// for the technician, one per line.
    /// </summary>
    public static class UpdateCheckFiles
    {
        public const string REQUEST_FILE_NAME = "update-check.request";
        public const string RESULT_FILE_NAME = "update-check.result";

        public static string FormatResult(Guid requestId, UpdateCheckOutcome outcome, string message)
        {
            string oneLineMessage = message.Replace("\r", " ").Replace("\n", " ");
            return requestId.ToString("D") + "\n" + outcome + "\n" + oneLineMessage;
        }

        /// <summary>The result for <paramref name="requestId"/>, or false when the text is missing, unreadable or answers another request.</summary>
        public static bool TryReadResult(string? resultText, Guid requestId, out UpdateCheckOutcome outcome, out string message)
        {
            outcome = UpdateCheckOutcome.Failed;
            message = "";
            if (resultText == null)
            {
                return false;
            }
            string[] lines = resultText.Replace("\r\n", "\n").Split('\n');
            if (lines.Length < 3 || !Guid.TryParse(lines[0].Trim(), out Guid answeredRequestId) || answeredRequestId != requestId)
            {
                return false;
            }
            if (!Enum.TryParse(lines[1].Trim(), out outcome))
            {
                return false;
            }
            message = lines[2].Trim();
            return true;
        }

        /// <summary>The id in a request file, or null when it holds none.</summary>
        public static Guid? ReadRequest(string? requestText)
        {
            return requestText != null && Guid.TryParse(requestText.Trim(), out Guid requestId) ? requestId : (Guid?)null;
        }
    }
}
