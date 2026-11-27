using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Addin.Updates;
using Microsoft.Win32;

namespace Uploader
{
    /// <summary>
    /// Asks the website, when the uploader starts and then hourly, for the newest add-in release and for the parameter
    /// standard (engineer, 2026-11-27: updates arrive through the uploader, and Adnan decides when a version is released
    /// by publishing it on the website). A newer release is checked and installed by AddinUpdates; the add-in then tells
    /// the technician to restart Revit. The website's endpoints, agreed 2026-11-27:
    ///   GET /api/addin/latest              {"version","sha256","size","releasedAt"}, or 404 when nothing is published
    ///   GET /api/addin/download/{version}  the package zip as Build-Package.ps1 writes it
    ///   GET /api/parameter-standard        the standard as JSON (see src/Addin/Standard/StandardContent.cs), or 404
    /// Any failure here is logged and tried again at the next check; it never stops the sending of snapshots.
    /// </summary>
    internal sealed class UpdateCheck
    {
        private const string LATEST_RELEASE_PATH = "/api/addin/latest";
        private const string DOWNLOAD_PATH = "/api/addin/download/";
        private const string PARAMETER_STANDARD_PATH = "/api/parameter-standard";
        private const string STARTUP_REGISTRY_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private static readonly TimeSpan TIME_BETWEEN_CHECKS = TimeSpan.FromHours(1);
        // How long a newly started uploader must stay running before the old one leaves it the work.
        private static readonly TimeSpan NEW_UPLOADER_PROOF_TIME = TimeSpan.FromSeconds(10);

        private readonly string productName;
        private readonly string localProductFolder;
        private readonly string addinsRootFolder;
        private readonly bool mayChangeStartAtSignIn;
        private readonly bool mayHandOver;
        private readonly IReadOnlyList<string> successorArguments;
        private readonly Action<string> log;
        private DateTime nextCheckUtc = DateTime.MinValue;
        private Version? uploaderVersionThatFailedToStart;

        /// <param name="mayChangeStartAtSignIn">False for a trial against scratch folders: the registry is never touched.</param>
        /// <param name="mayHandOver">False for a single pass (--once): the new uploader is installed but not started.</param>
        /// <param name="successorArguments">This copy's own arguments (a trial's folder overrides), passed on to its successor.</param>
        public UpdateCheck(string productName, string localAppDataFolder, string roamingAppDataFolder, bool mayChangeStartAtSignIn,
            bool mayHandOver, IReadOnlyList<string> successorArguments, Action<string> log)
        {
            this.productName = productName;
            localProductFolder = Path.Combine(localAppDataFolder, productName);
            addinsRootFolder = Path.Combine(roamingAppDataFolder, "Autodesk", "Revit", "Addins");
            this.mayChangeStartAtSignIn = mayChangeStartAtSignIn;
            this.mayHandOver = mayHandOver;
            this.successorArguments = successorArguments;
            this.log = log;
        }

        public static Version? RunningVersion => ReleaseVersions.Parse(
            typeof(UpdateCheck).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion);

        private string StartupEntryName => productName + " uploader";

        public async Task RunWhenDueAsync(HttpClient http)
        {
            if (DateTime.UtcNow < nextCheckUtc)
            {
                return;
            }
            nextCheckUtc = DateTime.UtcNow + TIME_BETWEEN_CHECKS;
            try
            {
                await SaveParameterStandardAsync(http);
            }
            catch (Exception exception)
            {
                log($"parameter standard: not received, trying again in an hour: {exception.GetType().Name}: {exception.Message}");
            }
            try
            {
                await InstallNewerReleaseAsync(http);
            }
            catch (Exception exception)
            {
                log($"update: failed, trying again in an hour: {exception.GetType().Name}: {exception.Message}");
            }
        }

        /// <summary>Saves the website's standard for the Parameter standard window, when it is complete and has changed.</summary>
        private async Task SaveParameterStandardAsync(HttpClient http)
        {
            using HttpResponseMessage response = await http.GetAsync(PARAMETER_STANDARD_PATH);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }
            response.EnsureSuccessStatusCode();
            string standardJson = await response.Content.ReadAsStringAsync();
            using (JsonDocument standard = JsonDocument.Parse(standardJson))
            {
                bool hasParameters = standard.RootElement.TryGetProperty("parameters", out JsonElement parameters)
                    && parameters.ValueKind == JsonValueKind.Array && parameters.GetArrayLength() > 0;
                if (!hasParameters)
                {
                    log("parameter standard: the website's copy has no parameters; kept the one saved before");
                    return;
                }
            }
            string standardPath = Path.Combine(localProductFolder, "parameter-standard.json");
            if (File.Exists(standardPath) && File.ReadAllText(standardPath) == standardJson)
            {
                return;
            }
            Directory.CreateDirectory(localProductFolder);
            string temporaryPath = standardPath + ".partial";
            await File.WriteAllTextAsync(temporaryPath, standardJson);
            File.Move(temporaryPath, standardPath, overwrite: true);
            log($"parameter standard: saved the website's copy to {standardPath}");
        }

        private async Task InstallNewerReleaseAsync(HttpClient http)
        {
            using HttpResponseMessage latestResponse = await http.GetAsync(LATEST_RELEASE_PATH);
            if (latestResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }
            latestResponse.EnsureSuccessStatusCode();
            using JsonDocument latest = JsonDocument.Parse(await latestResponse.Content.ReadAsStringAsync());
            string versionText = latest.RootElement.GetProperty("version").GetString() ?? "";
            string expectedSha256 = latest.RootElement.GetProperty("sha256").GetString() ?? "";
            long expectedSizeBytes = latest.RootElement.GetProperty("size").GetInt64();
            Version? latestVersion = ReleaseVersions.Parse(versionText);
            if (latestVersion == null || latestVersion.ToString() != versionText.Trim())
            {
                log($"update: the website's latest version \"{versionText}\" is not major.minor.patch; ignored");
                return;
            }

            Dictionary<string, Version?> installedAddins = AddinUpdates.InstalledVersions(addinsRootFolder, productName);
            bool isAddinOlder = installedAddins.Values.Any(installed => ReleaseVersions.IsNewer(latestVersion, installed));
            bool isUploaderOlder = ReleaseVersions.IsNewer(latestVersion, RunningVersion) && latestVersion != uploaderVersionThatFailedToStart;
            if (!isAddinOlder && !isUploaderOlder)
            {
                return;
            }

            string updatesFolder = Path.Combine(localProductFolder, "Updates");
            Directory.CreateDirectory(updatesFolder);
            string zipPath = Path.Combine(updatesFolder, $"{productName}-{latestVersion}.zip");
            if (!AddinUpdates.IsIntact(zipPath, expectedSizeBytes, expectedSha256))
            {
                string temporaryZipPath = zipPath + ".partial";
                using (HttpResponseMessage download = await http.GetAsync(DOWNLOAD_PATH + latestVersion))
                {
                    download.EnsureSuccessStatusCode();
                    await File.WriteAllBytesAsync(temporaryZipPath, await download.Content.ReadAsByteArrayAsync());
                }
                if (!AddinUpdates.IsIntact(temporaryZipPath, expectedSizeBytes, expectedSha256))
                {
                    File.Delete(temporaryZipPath);
                    log($"update: the download of {latestVersion} does not match its size and checksum; nothing installed");
                    return;
                }
                File.Move(temporaryZipPath, zipPath, overwrite: true);
            }
            string packageFolder = Path.Combine(updatesFolder, latestVersion.ToString());
            AddinUpdates.Unpack(zipPath, packageFolder);

            if (isAddinOlder)
            {
                List<string> updatedYears = AddinUpdates.InstallAddin(packageFolder, addinsRootFolder, productName, latestVersion);
                log(updatedYears.Count > 0
                    ? $"update: add-in {latestVersion} installed for Revit {string.Join(", ", updatedYears)}; it starts at the next Revit start"
                    : $"update: add-in {latestVersion} has no build for the Revit years installed here");
            }
            if (isUploaderOlder)
            {
                string programFileName = productName + ".Uploader.exe";
                string? newUploaderPath = AddinUpdates.InstallUploader(packageFolder, Path.Combine(localProductFolder, "Uploader"), programFileName, latestVersion);
                if (newUploaderPath != null)
                {
                    HandOverTo(newUploaderPath, latestVersion);
                }
            }
        }

        /// <summary>
        /// Starts the new uploader and ends this one once the new one has stayed up for a few seconds; if it stops at once,
        /// this one carries on and does not try that version again until it restarts. The new one takes over the start at
        /// sign-in only after its first full pass (StartAtSignInFromHere).
        /// </summary>
        private void HandOverTo(string newUploaderPath, Version newVersion)
        {
            if (!mayHandOver)
            {
                log($"update: uploader {newVersion} installed at {newUploaderPath}; not started (single pass)");
                return;
            }
            log($"update: starting uploader {newVersion} ({newUploaderPath})");
            var startInfo = new ProcessStartInfo(newUploaderPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(newUploaderPath)! };
            foreach (string argument in successorArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            startInfo.ArgumentList.Add("--after");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            using Process? newUploader = Process.Start(startInfo);
            if (newUploader == null || newUploader.WaitForExit(NEW_UPLOADER_PROOF_TIME))
            {
                uploaderVersionThatFailedToStart = newVersion;
                log($"update: uploader {newVersion} stopped at once; this one ({RunningVersion}) carries on");
                return;
            }
            log($"update: uploader {newVersion} is running; this one ({RunningVersion}) ends");
            Environment.Exit(0);
        }

        /// <summary>
        /// After a full pass, points the start at sign-in (the user's Run entry, set by the installer) at this copy when it
        /// points at an older one, and deletes uploader versions older than the one before. A missing entry is left
        /// missing: the technician may have switched the uploader off.
        /// </summary>
        public void StartAtSignInFromHere()
        {
            if (!mayChangeStartAtSignIn)
            {
                return;
            }
            string ownPath = Environment.ProcessPath ?? "";
            Version? ownVersion = RunningVersion;
            using RegistryKey? startupKey = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, writable: true);
            string? registeredCommand = startupKey?.GetValue(StartupEntryName) as string;
            if (startupKey == null || registeredCommand == null || ownVersion == null)
            {
                return;
            }
            string registeredPath = registeredCommand.Trim().Trim('"');
            if (string.Equals(registeredPath, ownPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            Version? registeredVersion = ReleaseVersions.Parse(Path.GetFileName(Path.GetDirectoryName(registeredPath)));
            if (!ReleaseVersions.IsNewer(ownVersion, registeredVersion))
            {
                return;
            }
            startupKey.SetValue(StartupEntryName, "\"" + ownPath + "\"");
            log($"update: Windows now starts uploader {ownVersion} at sign-in ({ownPath})");
            AddinUpdates.DeleteOlderVersions(Path.Combine(localProductFolder, "Uploader"), ownVersion);
        }
    }
}
