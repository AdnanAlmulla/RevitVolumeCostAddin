// Snapshot classes, schema version 1 (2026-11-26). Shared with the website, which compiles this folder from a clone
// of this repo beside it. Changes are additive only: add optional properties; renaming or removing one needs the
// engineer's say-so (CLAUDE.md "How work lands here"). Plain classes, no serializer attributes: the add-in writes
// them with its own JSON writer (no third-party files in Revit), the website reads them with System.Text.Json.
using System;
using System.Collections.Generic;

namespace Contracts
{
    /// <summary>
    /// One model as it stood at one moment: every element with a material volume, as facts only. The add-in never
    /// classifies (division, element kind, RC or PT): the website's per-project rules do, so a rule change applies
    /// to past snapshots too. Lengths are in metres, areas in m², volumes in m³ (converted once, in the add-in).
    /// </summary>
    public sealed class Snapshot
    {
        public const int CURRENT_SCHEMA_VERSION = 1;

        public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

        /// <summary>Made by the add-in when it takes the snapshot, so a retried upload is stored once.</summary>
        public Guid SnapshotId { get; set; }

        public DateTime TakenAtUtc { get; set; }

        /// <summary>The Revit user name of whoever synced.</summary>
        public string SyncedBy { get; set; } = "";

        /// <summary>"SynchronizeWithCentral" or "ManualSync" (SnapshotTriggers).</summary>
        public string Trigger { get; set; } = "";

        public string AddinVersion { get; set; } = "";

        public string RevitVersion { get; set; } = "";

        public string RevitBuild { get; set; } = "";

        /// <summary>Seconds the add-in took to read the model, for the "a sync is not slowed" check.</summary>
        public double ReadSeconds { get; set; }

        /// <summary>Elements Revit failed to read; they are missing from Elements, so a total may be short by them.</summary>
        public int UnreadableElementCount { get; set; }

        public ModelIdentity Model { get; set; } = new ModelIdentity();

        public List<SnapshotLevel> Levels { get; set; } = new List<SnapshotLevel>();

        /// <summary>Each type used by an element, once, with its project and shared parameters.</summary>
        public List<ElementTypeFacts> Types { get; set; } = new List<ElementTypeFacts>();

        public List<ElementFacts> Elements { get; set; } = new List<ElementFacts>();
    }

    public static class SnapshotTriggers
    {
        public const string SYNCHRONIZE_WITH_CENTRAL = "SynchronizeWithCentral";
        public const string MANUAL_SYNC = "ManualSync";
    }

    /// <summary>
    /// Which model this is. ACC cloud models have no file path, so a model is named by its ACC project and model ids;
    /// a model that is not in the cloud (a detached copy, say) has none, and the website names it by its title.
    /// </summary>
    public sealed class ModelIdentity
    {
        public Guid? AccProjectId { get; set; }

        public Guid? AccModelId { get; set; }

        public string? CloudRegion { get; set; }

        public string Title { get; set; } = "";

        public bool IsWorkshared { get; set; }

        public bool IsDetached { get; set; }

        public bool IsInCloud { get; set; }
    }

    public sealed class SnapshotLevel
    {
        public long ElementId { get; set; }

        public string Name { get; set; } = "";

        /// <summary>Revit's Level.Elevation, in metres.</summary>
        public double ElevationM { get; set; }

        /// <summary>Revit's Level.ProjectElevation (from the project base point), in metres.</summary>
        public double ProjectElevationM { get; set; }
    }

    public sealed class ElementTypeFacts
    {
        public long TypeId { get; set; }

        public string FamilyName { get; set; } = "";

        public string TypeName { get; set; } = "";

        public List<ParameterFact> Parameters { get; set; } = new List<ParameterFact>();
    }

    public sealed class ElementFacts
    {
        /// <summary>Revit's UniqueId: the element's key across snapshots.</summary>
        public string UniqueId { get; set; } = "";

        /// <summary>Revit's element id (64-bit), for display.</summary>
        public long ElementId { get; set; }

        public string Category { get; set; } = "";

        /// <summary>The category's built-in name (for example "OST_Walls"), the same in every language of Revit.</summary>
        public string BuiltInCategory { get; set; } = "";

        public long? TypeId { get; set; }

        public string FamilyName { get; set; } = "";

        public string TypeName { get; set; } = "";

        /// <summary>The element's own level, else its reference or base level.</summary>
        public string? LevelName { get; set; }

        /// <summary>Where the element starts and ends, for one that runs through several storeys.</summary>
        public string? BaseLevelName { get; set; }

        public string? TopLevelName { get; set; }

        /// <summary>Bottom and top of the element's bounding box, in metres, in the model's internal coordinates.</summary>
        public double? BottomElevationM { get; set; }

        public double? TopElevationM { get; set; }

        public List<MaterialQuantity> Materials { get; set; } = new List<MaterialQuantity>();

        /// <summary>Every project and shared instance parameter that has a value. Built-in parameters are not sent.</summary>
        public List<ParameterFact> Parameters { get; set; } = new List<ParameterFact>();
    }

    public sealed class MaterialQuantity
    {
        public string MaterialName { get; set; } = "";

        /// <summary>Revit's material class (for example "Concrete"); it does not always tell concrete from steel.</summary>
        public string MaterialClass { get; set; } = "";

        public double VolumeM3 { get; set; }

        public double AreaM2 { get; set; }
    }

    /// <summary>
    /// One parameter's value. Text is set for a text parameter; Number for a number, converted to metres, m² or m³
    /// when it is a length, area or volume (NumberUnit "m", "m2", "m3"), and as Revit stores it otherwise (NumberUnit
    /// null). Display is Revit's own text for it, in the project's units.
    /// </summary>
    public sealed class ParameterFact
    {
        public string Name { get; set; } = "";

        /// <summary>A shared parameter's GUID, the same in every project and Revit year; null for a project parameter.</summary>
        public Guid? SharedGuid { get; set; }

        public string? Text { get; set; }

        public double? Number { get; set; }

        public string? NumberUnit { get; set; }

        public string? Display { get; set; }
    }
}
