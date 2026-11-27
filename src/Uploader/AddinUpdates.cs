// Pure of the network and of Revit (file work only): compiled into tests/Addin.Tests by path.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Addin.Updates;

namespace Uploader
{
    /// <summary>
    /// Installs a newer release, downloaded from the website, while the technician keeps working (engineer, 2026-11-27).
    /// Every step leaves the working version in place until the new one is complete:
    ///   1. the download is checked against the size and SHA-256 the website gives before anything is unpacked;
    ///   2. it is unpacked, then copied, under a temporary name and renamed when complete;
    ///   3. for each Revit year that already has the add-in, the new version goes into its own folder,
    ///      Addins\&lt;year&gt;\&lt;product&gt;\&lt;version&gt;\, beside the running one (Revit holds the running DLL), and the year's
    ///      .addin manifest is then replaced in one step to point at it, so the next Revit start loads it;
    ///   4. the version before is kept, for going back by hand; older ones are deleted when nothing holds them.
    /// The uploader's own new copy goes beside it the same way (Uploader\&lt;version&gt;\).
    /// </summary>
    public static class AddinUpdates
    {
        private const string TEMPORARY_SUFFIX = ".partial";
        private static readonly Regex REVIT_YEAR_FOLDER = new Regex(@"^\d{4}$");

        /// <summary>Whether a downloaded file has the size and SHA-256 (lowercase hex) the website gave for it.</summary>
        public static bool IsIntact(string filePath, long expectedSizeBytes, string expectedSha256)
        {
            var file = new FileInfo(filePath);
            if (!file.Exists || file.Length != expectedSizeBytes)
            {
                return false;
            }
            using FileStream stream = file.OpenRead();
            string actualSha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            return string.Equals(actualSha256, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Unpacks a checked package into <paramref name="packageFolder"/>, unless a complete copy is there already.</summary>
        public static void Unpack(string zipPath, string packageFolder)
        {
            if (Directory.Exists(packageFolder))
            {
                return;
            }
            string temporaryFolder = packageFolder + TEMPORARY_SUFFIX;
            DeleteIfPresent(temporaryFolder);
            // ExtractToDirectory refuses an entry that would land outside the folder.
            ZipFile.ExtractToDirectory(zipPath, temporaryFolder);
            Directory.Move(temporaryFolder, packageFolder);
        }

        /// <summary>The versions each installed Revit year's manifest points at, by year (null: a manifest with no version folder).</summary>
        public static Dictionary<string, Version?> InstalledVersions(string addinsRootFolder, string productName)
        {
            var versionByYear = new Dictionary<string, Version?>();
            if (!Directory.Exists(addinsRootFolder))
            {
                return versionByYear;
            }
            foreach (string yearFolder in Directory.GetDirectories(addinsRootFolder).Where(IsRevitYearFolder))
            {
                string manifestPath = Path.Combine(yearFolder, productName + ".addin");
                if (File.Exists(manifestPath))
                {
                    versionByYear[Path.GetFileName(yearFolder)] = ReleaseVersions.FromManifest(File.ReadAllText(manifestPath));
                }
            }
            return versionByYear;
        }

        /// <summary>
        /// Installs the add-in from an unpacked package into each Revit year that has it installed and an older version, and
        /// returns those years. A year the package has no build for, or with no add-in installed, is left alone.
        /// </summary>
        public static List<string> InstallAddin(string packageFolder, string addinsRootFolder, string productName, Version version)
        {
            var updatedYears = new List<string>();
            foreach (KeyValuePair<string, Version?> installed in InstalledVersions(addinsRootFolder, productName))
            {
                string revitYear = installed.Key;
                if (!ReleaseVersions.IsNewer(version, installed.Value))
                {
                    continue;
                }
                string packageYearFolder = Path.Combine(packageFolder, revitYear);
                string packageVersionFolder = Path.Combine(packageYearFolder, productName, version.ToString());
                string packageManifest = Path.Combine(packageYearFolder, productName + ".addin");
                if (!Directory.Exists(packageVersionFolder) || !File.Exists(packageManifest))
                {
                    continue;
                }
                string productFolder = Path.Combine(addinsRootFolder, revitYear, productName);
                CopyFolderInOneStep(packageVersionFolder, Path.Combine(productFolder, version.ToString()));
                ReplaceFileInOneStep(packageManifest, Path.Combine(addinsRootFolder, revitYear, productName + ".addin"));
                DeleteOlderVersions(productFolder, version);
                updatedYears.Add(revitYear);
            }
            return updatedYears;
        }

        /// <summary>
        /// Copies the package's uploader to Uploader\&lt;version&gt;\ and returns the new program's path, or null when the package
        /// holds no uploader.
        /// </summary>
        public static string? InstallUploader(string packageFolder, string uploaderRootFolder, string programFileName, Version version)
        {
            string packageUploaderFolder = Path.Combine(packageFolder, "Uploader");
            if (!File.Exists(Path.Combine(packageUploaderFolder, programFileName)))
            {
                return null;
            }
            string newUploaderFolder = Path.Combine(uploaderRootFolder, version.ToString());
            CopyFolderInOneStep(packageUploaderFolder, newUploaderFolder);
            return Path.Combine(newUploaderFolder, programFileName);
        }

        /// <summary>
        /// Deletes the version folders older than the newest one below <paramref name="currentVersion"/>, which is kept for
        /// going back. A folder Revit (or a running uploader) still holds can't be deleted: it is left for a later try.
        /// Folders not named as a version are never touched.
        /// </summary>
        public static void DeleteOlderVersions(string versionsFolder, Version currentVersion)
        {
            if (!Directory.Exists(versionsFolder))
            {
                return;
            }
            List<KeyValuePair<Version, string>> olderVersions = Directory.GetDirectories(versionsFolder)
                .Select(folder => new KeyValuePair<Version?, string>(ReleaseVersions.Parse(Path.GetFileName(folder)), folder))
                .Where(entry => entry.Key != null && entry.Key < currentVersion && Path.GetFileName(entry.Value) == entry.Key.ToString())
                .Select(entry => new KeyValuePair<Version, string>(entry.Key!, entry.Value))
                .OrderByDescending(entry => entry.Key)
                .ToList();
            foreach (KeyValuePair<Version, string> olderVersion in olderVersions.Skip(1))
            {
                try
                {
                    Directory.Delete(olderVersion.Value, recursive: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                }
            }
        }

        private static bool IsRevitYearFolder(string folder)
        {
            return REVIT_YEAR_FOLDER.IsMatch(Path.GetFileName(folder));
        }

        /// <summary>Copies a folder under a temporary name, then renames it; a folder already complete is kept as it is.</summary>
        private static void CopyFolderInOneStep(string sourceFolder, string targetFolder)
        {
            if (Directory.Exists(targetFolder))
            {
                return;
            }
            string temporaryFolder = targetFolder + TEMPORARY_SUFFIX;
            DeleteIfPresent(temporaryFolder);
            CopyFolder(sourceFolder, temporaryFolder);
            Directory.Move(temporaryFolder, targetFolder);
        }

        private static void CopyFolder(string sourceFolder, string targetFolder)
        {
            Directory.CreateDirectory(targetFolder);
            foreach (string sourceFile in Directory.GetFiles(sourceFolder))
            {
                File.Copy(sourceFile, Path.Combine(targetFolder, Path.GetFileName(sourceFile)));
            }
            foreach (string sourceSubfolder in Directory.GetDirectories(sourceFolder))
            {
                CopyFolder(sourceSubfolder, Path.Combine(targetFolder, Path.GetFileName(sourceSubfolder)));
            }
        }

        /// <summary>Writes the new file beside the old one, then swaps it in with a single rename.</summary>
        private static void ReplaceFileInOneStep(string sourceFile, string targetFile)
        {
            string temporaryFile = targetFile + TEMPORARY_SUFFIX;
            File.Copy(sourceFile, temporaryFile, overwrite: true);
            File.Move(temporaryFile, targetFile, overwrite: true);
        }

        private static void DeleteIfPresent(string folder)
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
