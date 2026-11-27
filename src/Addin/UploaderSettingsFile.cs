// Pure code (no Revit API): compiled into tests/Addin.Tests by path.
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Addin
{
    /// <summary>
    /// Reads the website's address from the uploader's settings, %AppData%\&lt;product&gt;\uploader.json (written by the
    /// installer; src/Uploader/Program.cs owns the file), so the Help button can open the website. Only the address: the
    /// add-in never reads the site password. No JSON library: the add-in carries no third-party files.
    /// </summary>
    public static class UploaderSettingsFile
    {
        private static readonly Regex SITE_ADDRESS = new Regex("\"siteAddress\"\\s*:\\s*\"(?<address>[^\"]*)\"", RegexOptions.IgnoreCase);

        public static string PathFor(string productName)
        {
            string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(roamingAppData, productName, "uploader.json");
        }

        /// <summary>The address, or null when it is missing, blank or not a web address.</summary>
        public static string? SiteAddressFrom(string? settingsText)
        {
            if (settingsText == null)
            {
                return null;
            }
            Match match = SITE_ADDRESS.Match(settingsText);
            if (!match.Success)
            {
                return null;
            }
            string address = match.Groups["address"].Value.Trim();
            bool isWebAddress = address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || address.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            return isWebAddress ? address : null;
        }
    }
}
