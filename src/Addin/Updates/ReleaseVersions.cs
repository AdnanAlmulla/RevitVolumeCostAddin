// Pure code (no Revit API): compiled into the uploader and into tests/Addin.Tests by path.
using System;
using System.Text.RegularExpressions;

namespace Addin.Updates
{
    /// <summary>
    /// Release numbers (major.minor.patch, compared as numbers: 0.10.0 is newer than 0.9.0) and the version a Revit year's
    /// .addin manifest points at. From 0.7.0 each version is installed in its own folder,
    /// Addins\&lt;year&gt;\&lt;product&gt;\&lt;version&gt;\, and the manifest's Assembly path names it; Revit reads the manifest only when
    /// it starts, so pointing the manifest at a new folder is how an update is installed while Revit runs.
    /// </summary>
    public static class ReleaseVersions
    {
        // The leading major.minor.patch of a version text; anything after (such as "+<commit>") is ignored.
        private static readonly Regex RELEASE_NUMBER = new Regex(@"^\s*(?<number>\d+\.\d+\.\d+)");
        // The version folder in a manifest's Assembly path: <product>\<version>\<product>.dll
        private static readonly Regex MANIFEST_ASSEMBLY_VERSION = new Regex(@"<Assembly>[^<]*?[\\/](?<number>\d+\.\d+\.\d+)[\\/][^<\\/]+\.dll\s*</Assembly>", RegexOptions.IgnoreCase);

        /// <summary>The release number in a version text, or null when it has none.</summary>
        public static Version? Parse(string? versionText)
        {
            if (versionText == null)
            {
                return null;
            }
            Match match = RELEASE_NUMBER.Match(versionText);
            return match.Success ? new Version(match.Groups["number"].Value) : null;
        }

        /// <summary>The version a manifest's Assembly path points at, or null for a manifest with no version folder (before 0.7.0).</summary>
        public static Version? FromManifest(string? manifestText)
        {
            if (manifestText == null)
            {
                return null;
            }
            Match match = MANIFEST_ASSEMBLY_VERSION.Match(manifestText);
            return match.Success ? new Version(match.Groups["number"].Value) : null;
        }

        /// <summary>Whether <paramref name="candidate"/> is a newer release than <paramref name="current"/>; nothing is newer than an unknown.</summary>
        public static bool IsNewer(Version? candidate, Version? current)
        {
            return candidate != null && (current == null || candidate > current);
        }
    }
}
