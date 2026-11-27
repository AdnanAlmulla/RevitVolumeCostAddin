// Pure code (no Revit API, no WPF): compiled into tests/Addin.Tests by path.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Addin.Standard
{
    /// <summary>
    /// One whole version of the parameter standard as the Parameter standard window shows it: the website's copy when the
    /// uploader has received one, otherwise the one built into the add-in (ParameterStandard). Always one or the other in
    /// full, never a mix.
    /// </summary>
    public sealed class StandardContent
    {
        public StandardContent(string? websiteVersion, string agreedOn, IReadOnlyList<StandardParameter> parameters,
            string parameterSettings, IReadOnlyList<string> rules, IReadOnlyList<KeyValuePair<string, string>> examples)
        {
            WebsiteVersion = websiteVersion;
            AgreedOn = agreedOn;
            Parameters = parameters;
            ParameterSettings = parameterSettings;
            Rules = rules;
            Examples = examples;
        }

        /// <summary>The website's version stamp, or null for the copy built into the add-in.</summary>
        public string? WebsiteVersion { get; }

        public bool IsFromWebsite => WebsiteVersion != null;

        public string AgreedOn { get; }

        public IReadOnlyList<StandardParameter> Parameters { get; }

        public string ParameterSettings { get; }

        public IReadOnlyList<string> Rules { get; }

        /// <summary>Each example's element, then the values it carries.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Examples { get; }
    }

    /// <summary>
    /// The website's copy of the standard, which the uploader saves to %LocalAppData%\&lt;product&gt;\parameter-standard.json
    /// (GET /api/parameter-standard; the add-in itself never goes on the network). Read with .NET's own
    /// DataContractJsonSerializer, part of both .NET Framework 4.8 and .NET 8, so the add-in still carries no third-party file.
    /// </summary>
    public static class ParameterStandardFile
    {
        public const string FILE_NAME = "parameter-standard.json";

        public static string PathFor(string productName)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, productName, FILE_NAME);
        }

        /// <summary>The website's standard from its JSON, or null when the text is missing, unreadable or incomplete.</summary>
        public static StandardContent? Read(string? jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                return null;
            }
            WebsiteStandard? website;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(WebsiteStandard));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonText)))
                {
                    website = serializer.ReadObject(stream) as WebsiteStandard;
                }
            }
            catch (Exception)
            {
                return null;
            }
            return IsComplete(website) ? ToContent(website!) : null;
        }

        /// <summary>
        /// What a usable standard needs (agreed with the website, 2026-11-27): a version and a date, at least one parameter,
        /// and each parameter a name, at least one allowed value and at least one category.
        /// </summary>
        private static bool IsComplete(WebsiteStandard? website)
        {
            if (website == null || string.IsNullOrWhiteSpace(website.Version) || string.IsNullOrWhiteSpace(website.AgreedOn))
            {
                return false;
            }
            if (website.Parameters == null || website.Parameters.Count == 0)
            {
                return false;
            }
            return website.Parameters.All(parameter =>
                parameter != null
                && !string.IsNullOrWhiteSpace(parameter.Name)
                && parameter.AllowedValues != null && parameter.AllowedValues.Count > 0
                && parameter.Categories != null && parameter.Categories.Count > 0);
        }

        private static StandardContent ToContent(WebsiteStandard website)
        {
            List<StandardParameter> parameters = website.Parameters!.Select(parameter => new StandardParameter(
                parameter.Name!,
                parameter.Records ?? "",
                parameter.AllowedValues!,
                string.IsNullOrWhiteSpace(parameter.OtherValuesNote) ? null : parameter.OtherValuesNote,
                parameter.Categories!,
                parameter.FillOn ?? "",
                parameter.IfBlank ?? "")).ToList();
            List<KeyValuePair<string, string>> examples = (website.Examples ?? new List<WebsiteExample>())
                .Where(example => example != null)
                .Select(example => new KeyValuePair<string, string>(example.Element ?? "", example.Values ?? ""))
                .ToList();
            return new StandardContent(website.Version, website.AgreedOn!, parameters, website.ParameterSettings ?? "",
                website.Rules ?? new List<string>(), examples);
        }

        [DataContract]
        private sealed class WebsiteStandard
        {
            [DataMember(Name = "version")] public string? Version { get; set; }
            [DataMember(Name = "agreedOn")] public string? AgreedOn { get; set; }
            [DataMember(Name = "parameters")] public List<WebsiteParameter>? Parameters { get; set; }
            [DataMember(Name = "parameterSettings")] public string? ParameterSettings { get; set; }
            [DataMember(Name = "rules")] public List<string>? Rules { get; set; }
            [DataMember(Name = "examples")] public List<WebsiteExample>? Examples { get; set; }
        }

        [DataContract]
        private sealed class WebsiteParameter
        {
            [DataMember(Name = "name")] public string? Name { get; set; }
            [DataMember(Name = "records")] public string? Records { get; set; }
            [DataMember(Name = "allowedValues")] public List<string>? AllowedValues { get; set; }
            [DataMember(Name = "otherValuesNote")] public string? OtherValuesNote { get; set; }
            [DataMember(Name = "categories")] public List<string>? Categories { get; set; }
            [DataMember(Name = "fillOn")] public string? FillOn { get; set; }
            [DataMember(Name = "ifBlank")] public string? IfBlank { get; set; }
        }

        [DataContract]
        private sealed class WebsiteExample
        {
            [DataMember(Name = "element")] public string? Element { get; set; }
            [DataMember(Name = "values")] public string? Values { get; set; }
        }
    }
}
