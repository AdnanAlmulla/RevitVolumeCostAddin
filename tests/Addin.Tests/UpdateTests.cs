using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Addin.Standard;
using Addin.Updates;
using Uploader;
using Xunit;

namespace Addin.Tests
{
    /// <summary>
    /// Updates from the website: release numbers, the version a manifest points at, the checked download, installing beside
    /// the running version, and the website's copy of the parameter standard. All on temporary folders.
    /// </summary>
    public class UpdateTests : IDisposable
    {
        private const string PRODUCT = "Telltale";
        private readonly string scratchFolder = Path.Combine(Path.GetTempPath(), "update-test-" + Guid.NewGuid().ToString("N"));

        public UpdateTests()
        {
            Directory.CreateDirectory(scratchFolder);
        }

        public void Dispose()
        {
            Directory.Delete(scratchFolder, recursive: true);
        }

        private static string ManifestFor(string version)
        {
            return "<?xml version=\"1.0\"?>\n<RevitAddIns><AddIn Type=\"Application\"><Assembly>" + PRODUCT + "\\" + version + "\\" + PRODUCT + ".dll</Assembly></AddIn></RevitAddIns>";
        }

        /// <summary>An installed Revit year: its manifest and its version folder, as the installer leaves them.</summary>
        private string InstallYear(string addinsRoot, string revitYear, string version)
        {
            string yearFolder = Path.Combine(addinsRoot, revitYear);
            Directory.CreateDirectory(Path.Combine(yearFolder, PRODUCT, version));
            File.WriteAllText(Path.Combine(yearFolder, PRODUCT, version, PRODUCT + ".dll"), "dll " + version);
            File.WriteAllText(Path.Combine(yearFolder, PRODUCT + ".addin"), ManifestFor(version));
            return yearFolder;
        }

        /// <summary>An unpacked package of <paramref name="version"/> with builds for the given Revit years and an uploader.</summary>
        private string Package(string version, params string[] revitYears)
        {
            string packageFolder = Path.Combine(scratchFolder, "package-" + version);
            foreach (string revitYear in revitYears)
            {
                string versionFolder = Path.Combine(packageFolder, revitYear, PRODUCT, version);
                Directory.CreateDirectory(versionFolder);
                File.WriteAllText(Path.Combine(versionFolder, PRODUCT + ".dll"), "dll " + version);
                File.WriteAllText(Path.Combine(packageFolder, revitYear, PRODUCT + ".addin"), ManifestFor(version));
            }
            Directory.CreateDirectory(Path.Combine(packageFolder, "Uploader"));
            File.WriteAllText(Path.Combine(packageFolder, "Uploader", PRODUCT + ".Uploader.exe"), "exe " + version);
            return packageFolder;
        }

        [Theory]
        [InlineData("0.7.0", "0.7.0")]
        [InlineData("0.7.0+4c151c44ef5c", "0.7.0")]
        [InlineData("10.2.33", "10.2.33")]
        [InlineData("0.7", null)]
        [InlineData("latest", null)]
        [InlineData(null, null)]
        public void A_release_number_is_major_minor_patch(string? versionText, string? expected)
        {
            Assert.Equal(expected, ReleaseVersions.Parse(versionText)?.ToString());
        }

        [Fact]
        public void Release_numbers_compare_as_numbers()
        {
            Assert.True(ReleaseVersions.IsNewer(ReleaseVersions.Parse("0.10.0"), ReleaseVersions.Parse("0.9.0")));
            Assert.False(ReleaseVersions.IsNewer(ReleaseVersions.Parse("0.7.0"), ReleaseVersions.Parse("0.7.0")));
            Assert.False(ReleaseVersions.IsNewer(null, ReleaseVersions.Parse("0.7.0")));
            Assert.True(ReleaseVersions.IsNewer(ReleaseVersions.Parse("0.7.0"), null));
        }

        [Fact]
        public void A_manifest_names_the_version_it_loads()
        {
            Assert.Equal("0.7.1", ReleaseVersions.FromManifest(ManifestFor("0.7.1"))?.ToString());
            Assert.Null(ReleaseVersions.FromManifest("<Assembly>Telltale\\Telltale.dll</Assembly>"));
            Assert.Null(ReleaseVersions.FromManifest(null));
        }

        [Fact]
        public void A_download_is_intact_only_with_the_given_size_and_checksum()
        {
            string filePath = Path.Combine(scratchFolder, "download.zip");
            File.WriteAllText(filePath, "package bytes");
            byte[] bytes = File.ReadAllBytes(filePath);
            string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            Assert.True(AddinUpdates.IsIntact(filePath, bytes.Length, sha256));
            Assert.True(AddinUpdates.IsIntact(filePath, bytes.Length, sha256.ToUpperInvariant()));
            Assert.False(AddinUpdates.IsIntact(filePath, bytes.Length - 1, sha256));
            Assert.False(AddinUpdates.IsIntact(filePath, bytes.Length, new string('0', 64)));
            Assert.False(AddinUpdates.IsIntact(Path.Combine(scratchFolder, "missing.zip"), bytes.Length, sha256));
        }

        [Fact]
        public void A_newer_version_goes_beside_the_running_one_and_the_manifest_points_at_it()
        {
            string addinsRoot = Path.Combine(scratchFolder, "Addins");
            string year2026 = InstallYear(addinsRoot, "2026", "0.7.0");
            string package = Package("0.7.1", "2024", "2026");

            var updatedYears = AddinUpdates.InstallAddin(package, addinsRoot, PRODUCT, new Version(0, 7, 1));

            Assert.Equal(new[] { "2026" }, updatedYears);
            Assert.Equal("0.7.1", ReleaseVersions.FromManifest(File.ReadAllText(Path.Combine(year2026, PRODUCT + ".addin")))?.ToString());
            Assert.True(File.Exists(Path.Combine(year2026, PRODUCT, "0.7.1", PRODUCT + ".dll")));
            Assert.True(File.Exists(Path.Combine(year2026, PRODUCT, "0.7.0", PRODUCT + ".dll")));
            // Revit 2024 has no add-in installed on this PC, so the update leaves it alone.
            Assert.False(Directory.Exists(Path.Combine(addinsRoot, "2024")));
            Assert.Empty(Directory.GetFiles(year2026, "*.partial", SearchOption.AllDirectories));
        }

        [Fact]
        public void A_version_already_installed_or_newer_is_left_alone()
        {
            string addinsRoot = Path.Combine(scratchFolder, "Addins");
            string year2026 = InstallYear(addinsRoot, "2026", "0.7.2");
            string package = Package("0.7.1", "2026");

            var updatedYears = AddinUpdates.InstallAddin(package, addinsRoot, PRODUCT, new Version(0, 7, 1));

            Assert.Empty(updatedYears);
            Assert.Equal("0.7.2", ReleaseVersions.FromManifest(File.ReadAllText(Path.Combine(year2026, PRODUCT + ".addin")))?.ToString());
        }

        [Fact]
        public void Only_the_version_before_is_kept_for_going_back()
        {
            string versionsFolder = Path.Combine(scratchFolder, "versions");
            foreach (string version in new[] { "0.6.9", "0.7.0", "0.7.1", "0.7.2" })
            {
                Directory.CreateDirectory(Path.Combine(versionsFolder, version));
            }
            Directory.CreateDirectory(Path.Combine(versionsFolder, "notes"));

            AddinUpdates.DeleteOlderVersions(versionsFolder, new Version(0, 7, 2));

            string[] kept = Directory.GetDirectories(versionsFolder).Select(Path.GetFileName).OrderBy(name => name).ToArray()!;
            Assert.Equal(new[] { "0.7.1", "0.7.2", "notes" }, kept);
        }

        [Fact]
        public void The_new_uploader_goes_in_its_own_version_folder()
        {
            string package = Package("0.7.1", "2026");
            string uploaderRoot = Path.Combine(scratchFolder, "Uploader");

            string? newProgram = AddinUpdates.InstallUploader(package, uploaderRoot, PRODUCT + ".Uploader.exe", new Version(0, 7, 1));

            Assert.Equal(Path.Combine(uploaderRoot, "0.7.1", PRODUCT + ".Uploader.exe"), newProgram);
            Assert.Equal("exe 0.7.1", File.ReadAllText(newProgram!));
        }

        [Fact]
        public void A_package_is_unpacked_whole_into_its_folder()
        {
            string sourceFolder = Package("0.7.1", "2026");
            string zipPath = Path.Combine(scratchFolder, "Telltale-0.7.1.zip");
            ZipFile.CreateFromDirectory(sourceFolder, zipPath);
            string unpackedFolder = Path.Combine(scratchFolder, "Updates", "0.7.1");

            AddinUpdates.Unpack(zipPath, unpackedFolder);

            Assert.True(File.Exists(Path.Combine(unpackedFolder, "2026", PRODUCT + ".addin")));
            Assert.False(Directory.Exists(unpackedFolder + ".partial"));
        }

        private const string WEBSITE_STANDARD =
            "{\"version\":\"a1b2c3d4e5f6\",\"agreedOn\":\"28 Nov 2026\"," +
            "\"parameters\":[{\"name\":\"Division_Shared\",\"records\":\"Which division.\",\"allowedValues\":[\"Piles\",\"Roof\"],\"otherValuesNote\":null," +
            "\"categories\":[\"Walls\"],\"fillOn\":\"Every structural element.\",\"ifBlank\":\"Division \\\"Unassigned\\\"\"}]," +
            "\"parameterSettings\":\"Shared, instance, text.\",\"rules\":[\"Spell exactly.\"]," +
            "\"examples\":[{\"element\":\"A core wall\",\"values\":\"Division_Shared = Tower\"}]}";

        [Fact]
        public void The_websites_standard_is_read_whole()
        {
            StandardContent? standard = ParameterStandardFile.Read(WEBSITE_STANDARD);

            Assert.NotNull(standard);
            Assert.True(standard!.IsFromWebsite);
            Assert.Equal("a1b2c3d4e5f6", standard.WebsiteVersion);
            Assert.Equal("28 Nov 2026", standard.AgreedOn);
            Assert.Equal(new[] { "Piles", "Roof" }, standard.Parameters.Single().AllowedValues);
            Assert.Null(standard.Parameters.Single().OtherValuesNote);
            Assert.Equal("Division \"Unassigned\"", standard.Parameters.Single().IfBlank);
            Assert.Equal("Division_Shared = Tower", standard.Examples.Single().Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{\"version\":\"a\",\"agreedOn\":\"b\",\"parameters\":[]}")]
        [InlineData("{\"agreedOn\":\"b\",\"parameters\":[{\"name\":\"X\",\"allowedValues\":[\"A\"],\"categories\":[\"Walls\"]}]}")]
        [InlineData("{\"version\":\"a\",\"agreedOn\":\"b\",\"parameters\":[{\"name\":\"X\",\"allowedValues\":[],\"categories\":[\"Walls\"]}]}")]
        [InlineData("{\"version\":\"a\",\"agreedOn\":\"b\",\"parameters\":[{\"name\":\"X\",\"allowedValues\":[\"A\"]}]}")]
        public void An_incomplete_website_standard_is_not_used(string? jsonText)
        {
            Assert.Null(ParameterStandardFile.Read(jsonText));
        }

        [Fact]
        public void The_uploaders_answer_reads_back_for_the_request_that_asked()
        {
            Guid requestId = Guid.NewGuid();
            string resultText = UpdateCheckFiles.FormatResult(requestId, UpdateCheckOutcome.Installed, "Version 0.7.2 is installed\nand starts next time.");

            bool isRead = UpdateCheckFiles.TryReadResult(resultText, requestId, out UpdateCheckOutcome outcome, out string message);

            Assert.True(isRead);
            Assert.Equal(UpdateCheckOutcome.Installed, outcome);
            Assert.Equal("Version 0.7.2 is installed and starts next time.", message);
        }

        [Fact]
        public void An_answer_to_another_request_or_a_broken_one_is_not_read()
        {
            Guid requestId = Guid.NewGuid();
            string otherAnswer = UpdateCheckFiles.FormatResult(Guid.NewGuid(), UpdateCheckOutcome.UpToDate, "Up to date.");

            Assert.False(UpdateCheckFiles.TryReadResult(otherAnswer, requestId, out _, out _));
            Assert.False(UpdateCheckFiles.TryReadResult(null, requestId, out _, out _));
            Assert.False(UpdateCheckFiles.TryReadResult(requestId + "\nNotAnOutcome\nText", requestId, out _, out _));
            Assert.False(UpdateCheckFiles.TryReadResult(requestId + "\nUpToDate", requestId, out _, out _));
        }

        [Fact]
        public void A_request_holds_its_id()
        {
            Guid requestId = Guid.NewGuid();

            Assert.Equal(requestId, UpdateCheckFiles.ReadRequest(requestId.ToString("D") + "\r\n"));
            Assert.Null(UpdateCheckFiles.ReadRequest("not an id"));
            Assert.Null(UpdateCheckFiles.ReadRequest(null));
        }

        [Fact]
        public void The_built_in_standard_says_it_is_not_the_websites()
        {
            Assert.False(ParameterStandard.BuiltIn.IsFromWebsite);
            Assert.Equal(ParameterStandard.PARAMETERS.Count, ParameterStandard.BuiltIn.Parameters.Count);
        }
    }
}
