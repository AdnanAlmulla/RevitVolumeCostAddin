// Pure code (no Revit API): compiled into tests/Addin.Tests by path.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Addin.Json;
using Contracts;

namespace Addin.Snapshots
{
    /// <summary>
    /// Writes a Contracts.Snapshot as JSON with the add-in's own writer (no third-party files in Revit). Property names
    /// are the classes' names in camel case, as System.Text.Json on the website reads them; a test reads this output
    /// back strictly into the same classes.
    /// </summary>
    public static class SnapshotJson
    {
        public static void Write(Snapshot snapshot, TextWriter output)
        {
            var json = new JsonWriter(output);
            json.BeginObject();
            json.Property("schemaVersion", (long)snapshot.SchemaVersion);
            json.Property("snapshotId", snapshot.SnapshotId.ToString());
            json.Property("takenAtUtc", snapshot.TakenAtUtc.ToString("o", CultureInfo.InvariantCulture));
            json.Property("syncedBy", snapshot.SyncedBy);
            json.Property("trigger", snapshot.Trigger);
            json.Property("addinVersion", snapshot.AddinVersion);
            json.Property("revitVersion", snapshot.RevitVersion);
            json.Property("revitBuild", snapshot.RevitBuild);
            json.Property("readSeconds", snapshot.ReadSeconds);
            json.Property("unreadableElementCount", (long)snapshot.UnreadableElementCount);
            WriteModel(json, snapshot.Model);
            WriteLevels(json, snapshot.Levels);
            WriteTypes(json, snapshot.Types);
            WriteElements(json, snapshot.Elements);
            json.EndObject();
        }

        private static void WriteModel(JsonWriter json, ModelIdentity model)
        {
            json.PropertyName("model");
            json.BeginObject();
            json.Property("accProjectId", model.AccProjectId?.ToString());
            json.Property("accModelId", model.AccModelId?.ToString());
            json.Property("cloudRegion", model.CloudRegion);
            json.Property("title", model.Title);
            json.Property("isWorkshared", model.IsWorkshared);
            json.Property("isDetached", model.IsDetached);
            json.Property("isInCloud", model.IsInCloud);
            json.EndObject();
        }

        private static void WriteLevels(JsonWriter json, List<SnapshotLevel> levels)
        {
            json.PropertyName("levels");
            json.BeginArray();
            foreach (SnapshotLevel level in levels)
            {
                json.BeginObject();
                json.Property("elementId", level.ElementId);
                json.Property("name", level.Name);
                json.Property("elevationM", level.ElevationM);
                json.Property("projectElevationM", level.ProjectElevationM);
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteTypes(JsonWriter json, List<ElementTypeFacts> types)
        {
            json.PropertyName("types");
            json.BeginArray();
            foreach (ElementTypeFacts type in types)
            {
                json.BeginObject();
                json.Property("typeId", type.TypeId);
                json.Property("familyName", type.FamilyName);
                json.Property("typeName", type.TypeName);
                WriteParameters(json, type.Parameters);
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteElements(JsonWriter json, List<ElementFacts> elements)
        {
            json.PropertyName("elements");
            json.BeginArray();
            foreach (ElementFacts element in elements)
            {
                json.BeginObject();
                json.Property("uniqueId", element.UniqueId);
                json.Property("elementId", element.ElementId);
                json.Property("category", element.Category);
                json.Property("builtInCategory", element.BuiltInCategory);
                WriteNullableNumber(json, "typeId", element.TypeId);
                json.Property("familyName", element.FamilyName);
                json.Property("typeName", element.TypeName);
                json.Property("levelName", element.LevelName);
                json.Property("baseLevelName", element.BaseLevelName);
                json.Property("topLevelName", element.TopLevelName);
                WriteNullableNumber(json, "bottomElevationM", element.BottomElevationM);
                WriteNullableNumber(json, "topElevationM", element.TopElevationM);
                json.PropertyName("materials");
                json.BeginArray();
                foreach (MaterialQuantity material in element.Materials)
                {
                    json.BeginObject();
                    json.Property("materialName", material.MaterialName);
                    json.Property("materialClass", material.MaterialClass);
                    json.Property("volumeM3", material.VolumeM3);
                    json.Property("areaM2", material.AreaM2);
                    json.EndObject();
                }
                json.EndArray();
                WriteParameters(json, element.Parameters);
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteParameters(JsonWriter json, List<ParameterFact> parameters)
        {
            json.PropertyName("parameters");
            json.BeginArray();
            foreach (ParameterFact parameter in parameters)
            {
                json.BeginObject();
                json.Property("name", parameter.Name);
                json.Property("sharedGuid", parameter.SharedGuid?.ToString());
                json.Property("text", parameter.Text);
                WriteNullableNumber(json, "number", parameter.Number);
                json.Property("numberUnit", parameter.NumberUnit);
                json.Property("display", parameter.Display);
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteNullableNumber(JsonWriter json, string name, double? number)
        {
            json.PropertyName(name);
            if (number.HasValue)
            {
                json.NumberValue(number.Value);
            }
            else
            {
                json.NullValue();
            }
        }

        private static void WriteNullableNumber(JsonWriter json, string name, long? number)
        {
            json.PropertyName(name);
            if (number.HasValue)
            {
                json.NumberValue(number.Value);
            }
            else
            {
                json.NullValue();
            }
        }
    }
}
