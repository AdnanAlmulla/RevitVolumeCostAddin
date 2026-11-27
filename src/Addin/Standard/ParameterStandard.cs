// Pure code (no Revit API, no WPF): compiled into tests/Addin.Tests by path.
using System.Collections.Generic;

namespace Addin.Standard
{
    /// <summary>One shared parameter the technicians fill in, as the parameter standard describes it.</summary>
    public sealed class StandardParameter
    {
        public StandardParameter(string name, string records, IReadOnlyList<string> allowedValues, string? otherValuesNote,
            IReadOnlyList<string> categories, string fillOn, string ifBlank)
        {
            Name = name;
            Records = records;
            AllowedValues = allowedValues;
            OtherValuesNote = otherValuesNote;
            Categories = categories;
            FillOn = fillOn;
            IfBlank = ifBlank;
        }

        /// <summary>The parameter's exact name in Revit.</summary>
        public string Name { get; }

        /// <summary>What it records.</summary>
        public string Records { get; }

        /// <summary>The values to type, spelled exactly.</summary>
        public IReadOnlyList<string> AllowedValues { get; }

        /// <summary>Values allowed beyond the list (a format), or null when only the list is allowed.</summary>
        public string? OtherValuesNote { get; }

        /// <summary>The Revit categories the parameter is bound to.</summary>
        public IReadOnlyList<string> Categories { get; }

        /// <summary>Which elements get a value.</summary>
        public string FillOn { get; }

        /// <summary>What the website shows for an element left blank.</summary>
        public string IfBlank { get; }
    }

    /// <summary>
    /// The parameter standard the BIM technicians follow: the shared instance parameters for quantity and cost, their
    /// allowed values, categories and rules. It is the add-in's own copy of the office's Excel sheet (website repo,
    /// docs/Revit parameter standard.xlsx), written once here and shown by the Parameters button, so the add-in installed on a
    /// technician's PC carries the version of the standard it was built with (engineer, 2026-11-27: "the plugin needs to
    /// carry the identity of the latest instance parameters").
    ///
    /// The snapshot does not depend on this list: it sends every parameter with its raw value, and the website reads
    /// them by name and checks the values (CLAUDE.md "Facts only"). A change here must also be made in the Excel sheet
    /// and on the website.
    /// </summary>
    public static class ParameterStandard
    {
        /// <summary>When the engineer agreed this version of the standard with the technicians.</summary>
        public const string AGREED_ON = "27 Nov 2026";

        private static readonly string[] CONCRETE_ELEMENT_CATEGORIES =
        {
            "Structural Columns", "Structural Framing", "Structural Foundations", "Floors", "Walls", "Generic Models", "Stairs",
        };

        public static readonly IReadOnlyList<StandardParameter> PARAMETERS = new[]
        {
            new StandardParameter(
                "Division_Shared",
                "Which division of the office's quantity table the element belongs to.",
                new[] { "Piles", "Foundation", "Basement", "Ground Floor", "Podium", "Tower", "Roof" },
                null,
                CONCRETE_ELEMENT_CATEGORIES,
                "Every structural element, concrete and steel. An element that runs through two divisions is split at the boundary first (rule 2).",
                "Division \"Unassigned\""),
            new StandardParameter(
                "Concrete Grade_Shared",
                "The concrete strength class of the element.",
                new[] { "C40/50", "C50/60", "C70/85", "Blinding" },
                "Other classes in the same format, such as C32/40.",
                CONCRETE_ELEMENT_CATEGORIES,
                "Every concrete element. Screed and mass concrete: Blinding. Leave blank on steel, insulation and compressible materials.",
                "Grade \"Unassigned\""),
            new StandardParameter(
                "Structural System_Shared",
                "Whether a slab or beam is reinforced or post-tensioned.",
                new[] { "RC", "PT" },
                null,
                new[] { "Floors", "Structural Framing", "Generic Models" },
                "Slabs and beams. Drop panels (Generic Models): the same value as their slab.",
                "RC"),
            new StandardParameter(
                "Wall Function_Shared",
                "What a wall does.",
                new[] { "Retaining", "Shear", "Core", "Upstand" },
                null,
                new[] { "Walls" },
                "Every structural wall, EPS walls included.",
                "Wall (a plain wall)"),
            new StandardParameter(
                "Steel Grade_Shared",
                "The steel grade of a steel member.",
                new[] { "S275", "S355" },
                "Other grades in the same format, such as S460.",
                new[] { "Structural Columns", "Structural Framing" },
                "Every steel member, concrete-filled tube columns included (they also keep their concrete grade).",
                "Steel grade \"Unassigned\""),
        };

        public const string PARAMETER_SETTINGS =
            "Every parameter above is a Shared parameter, Instance (not Type), of type Text, in the Text group, bound to the " +
            "categories listed. Add them to the office shared parameter file once, so they keep the same GUID in every " +
            "project and Revit version.";

        public static readonly IReadOnlyList<string> RULES = new[]
        {
            "Spell every value exactly as listed: same capitals, same slash, no extra spaces. The website matches the text exactly; anything else shows as \"Unassigned\".",
            "An element that runs through two divisions (a wall or column from Basement up into Ground Floor, say) must be split at the division boundary, so each part carries its own division and its own volume. Keeping the volumes right is the modeller's responsibility.",
            "Fill the values on each element (instance), never on its type.",
            "Stairs: fill the parameters on the stair itself (Stairs category), not on its runs or landings. A stair is counted once, flights and landings together.",
            "Shoring walls are not counted, so they need no values.",
            "EPS walls count as walls and EPS slabs as slabs: fill them like any other wall or slab.",
            "Piles keep their type parameters Diameter and Depth as they are now; the website reads pile size from them.",
        };

        public static readonly IReadOnlyList<KeyValuePair<string, string>> EXAMPLES = new[]
        {
            new KeyValuePair<string, string>("A 400 mm core wall on Level 12", "Division_Shared = Tower;  Wall Function_Shared = Core;  Concrete Grade_Shared = C50/60"),
            new KeyValuePair<string, string>("A PT slab on Podium 02", "Division_Shared = Podium;  Structural System_Shared = PT;  Concrete Grade_Shared = C40/50"),
            new KeyValuePair<string, string>("An SHS 300x300x8 steel column", "Division_Shared = Tower;  Steel Grade_Shared = S355"),
        };
    }
}
