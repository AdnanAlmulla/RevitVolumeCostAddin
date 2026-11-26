using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Addin
{
    /// <summary>The product's name and version (from Directory.Build.props, through the assembly), and the add-in's log.</summary>
    internal static class AddinInfo
    {
        private static readonly Assembly ADDIN_ASSEMBLY = typeof(AddinInfo).Assembly;

        public static string ProductName => ADDIN_ASSEMBLY.GetName().Name ?? "Addin";

        public static string Version =>
            ADDIN_ASSEMBLY.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? ADDIN_ASSEMBLY.GetName().Version?.ToString()
            ?? "unknown";

        public static string AssemblyPath => ADDIN_ASSEMBLY.Location;

        /// <summary>
        /// One line per snapshot or failure in %LocalAppData%\&lt;product&gt;\Logs\addin.log: the technician sends it back
        /// with the snapshot, and it times each read (the "a sync is not slowed" check). Logging never stops Revit.
        /// </summary>
        public static void Log(string line)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logFolder = Path.Combine(localAppData, ProductName, "Logs");
                Directory.CreateDirectory(logFolder);
                string stamped = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + line + Environment.NewLine;
                File.AppendAllText(Path.Combine(logFolder, "addin.log"), stamped);
            }
            catch (Exception)
            {
                // A log that can't be written must not disturb Revit.
            }
        }
    }
}
