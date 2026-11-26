using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Addin.Json;
using Addin.RevitApi;
using Addin.Units;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace Addin.RawDump
{
    /// <summary>What a finished dump reports back to the person who ran it.</summary>
    internal sealed class RawDumpOutcome
    {
        public int ElementCount { get; set; }
        public int UnreadableCount { get; set; }
        public double ReadTimeSeconds { get; set; }
    }

    /// <summary>
    /// The one-off raw dump (backlog F-004): every model element of the host model, with its category, family,
    /// type, level, materials with volumes, and every parameter's name and value, written to one gzipped JSON
    /// file. It is for discovery: it shows what the office's models actually carry, before any parameter name
    /// or snapshot class is fixed. It only reads; the command runs in Revit's read-only transaction mode.
    ///
    /// Values are written as Revit stores them (internal units: feet, square feet, cubic feet, radians), each
    /// with Revit's own display text in the project's units and each parameter's data type, so any value can
    /// be converted later. Material volumes and areas are also given in m³ and m², through RevitInternalUnits.
    ///
    /// File layout (format "raw-dump-1"): about, model, displayUnits, projectInformation, parameterBindings,
    /// levels, materials, elements, types, parameterDefinitions, unreadable, summary. Each parameter value
    /// points at its definition by id ("definition"); the definitions are listed once, near the end.
    /// </summary>
    internal sealed class RawDumpWriter
    {
        private const string DUMP_FORMAT = "raw-dump-1";

        private readonly Document document;
        private readonly Application application;
        private readonly string addinVersion;

        // Filled while elements, types and materials are written; written once, near the end of the file.
        private readonly Dictionary<long, Parameter> firstParameterByDefinitionId = new Dictionary<long, Parameter>();
        private readonly Dictionary<long, ElementId> usedTypeIdsByValue = new Dictionary<long, ElementId>();
        private readonly SortedDictionary<string, int> elementCountByCategory = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<long, string>> unreadableIdsWithErrors = new List<KeyValuePair<long, string>>();

        public RawDumpWriter(Document document, Application application, string addinVersion)
        {
            this.document = document;
            this.application = application;
            this.addinVersion = addinVersion;
        }

        public RawDumpOutcome WriteTo(string filePath)
        {
            Stopwatch readTime = Stopwatch.StartNew();
            int elementCount;

            using (FileStream file = File.Create(filePath))
            using (var compressed = new GZipStream(file, CompressionLevel.Optimal))
            using (var text = new StreamWriter(compressed, new UTF8Encoding(false)))
            {
                var json = new JsonWriter(text);
                json.BeginObject();
                json.Property("dumpFormat", DUMP_FORMAT);
                WriteAbout(json);
                WriteModel(json);
                WriteDisplayUnits(json);
                WriteProjectInformation(json);
                WriteParameterBindings(json);
                WriteLevels(json);
                WriteMaterials(json);
                elementCount = WriteElements(json);
                WriteTypes(json);
                WriteParameterDefinitions(json);
                WriteUnreadable(json);
                readTime.Stop();
                WriteSummary(json, elementCount, readTime.Elapsed.TotalSeconds);
                json.EndObject();
            }

            return new RawDumpOutcome
            {
                ElementCount = elementCount,
                UnreadableCount = unreadableIdsWithErrors.Count,
                ReadTimeSeconds = readTime.Elapsed.TotalSeconds,
            };
        }

        private void WriteAbout(JsonWriter json)
        {
            json.PropertyName("about");
            json.BeginObject();
            json.Property("createdUtc", DateTime.UtcNow.ToString("o"));
            json.Property("addinVersion", addinVersion);
            json.Property("revitVersionNumber", application.VersionNumber);
            json.Property("revitSubVersionNumber", application.SubVersionNumber);
            json.Property("revitVersionBuild", application.VersionBuild);
            json.Property("revitVersionName", application.VersionName);
            json.Property("revitUserName", application.Username);
            json.EndObject();
        }

        private void WriteModel(JsonWriter json)
        {
            json.PropertyName("model");
            json.BeginObject();
            json.Property("title", document.Title);
            json.Property("pathName", document.PathName);
            json.Property("isWorkshared", document.IsWorkshared);
            json.Property("isDetached", document.IsDetached);
            json.Property("isModelInCloud", document.IsModelInCloud);
            if (document.IsModelInCloud)
            {
                // ACC cloud models have no file path; they are named by their project and model ids (CLAUDE.md).
                ModelPath cloudModelPath = document.GetCloudModelPath();
                json.Property("cloudRegion", cloudModelPath.Region);
                json.Property("cloudProjectId", cloudModelPath.GetProjectGUID().ToString());
                json.Property("cloudModelId", cloudModelPath.GetModelGUID().ToString());
            }
            json.EndObject();
        }

        private void WriteDisplayUnits(JsonWriter json)
        {
            // The units Revit's display text is written in, so each "display" value can be read correctly.
            Autodesk.Revit.DB.Units projectUnits = document.GetUnits();
            json.PropertyName("displayUnits");
            json.BeginObject();
            json.Property("length", projectUnits.GetFormatOptions(SpecTypeId.Length).GetUnitTypeId().TypeId);
            json.Property("area", projectUnits.GetFormatOptions(SpecTypeId.Area).GetUnitTypeId().TypeId);
            json.Property("volume", projectUnits.GetFormatOptions(SpecTypeId.Volume).GetUnitTypeId().TypeId);
            json.EndObject();
        }

        private void WriteProjectInformation(JsonWriter json)
        {
            ProjectInfo projectInformation = document.ProjectInformation;
            json.PropertyName("projectInformation");
            bool isWritten = TryWriteBuffered(json, projectInformation.Id, buffer =>
            {
                buffer.BeginObject();
                buffer.Property("id", ElementIds.ValueOf(projectInformation.Id));
                WriteParameters(buffer, projectInformation);
                buffer.EndObject();
            });
            if (!isWritten)
            {
                json.NullValue();
            }
        }

        private void WriteParameterBindings(JsonWriter json)
        {
            // Project and shared parameters added to the model, and the categories each one is bound to.
            json.PropertyName("parameterBindings");
            json.BeginArray();
            DefinitionBindingMapIterator binding = document.ParameterBindings.ForwardIterator();
            while (binding.MoveNext())
            {
                Definition definition = binding.Key;
                var elementBinding = binding.Current as ElementBinding;
                ElementId definitionId = definition is InternalDefinition internalDefinition ? internalDefinition.Id : ElementId.InvalidElementId;
                TryWriteBuffered(json, definitionId, buffer => WriteParameterBinding(buffer, definition, definitionId, elementBinding));
            }
            json.EndArray();
        }

        private static void WriteParameterBinding(JsonWriter json, Definition definition, ElementId definitionId, ElementBinding? elementBinding)
        {
            string bindingKind = elementBinding is InstanceBinding ? "instance" : elementBinding is TypeBinding ? "type" : "other";
            json.BeginObject();
            json.Property("name", definition.Name);
            json.Property("definition", ElementIds.ValueOf(definitionId));
            json.Property("dataType", definition.GetDataType().TypeId);
            json.Property("bindingKind", bindingKind);
            json.PropertyName("categories");
            json.BeginArray();
            if (elementBinding != null)
            {
                foreach (Category category in elementBinding.Categories)
                {
                    json.StringValue(category.Name);
                }
            }
            json.EndArray();
            json.EndObject();
        }

        private void WriteLevels(JsonWriter json)
        {
            json.PropertyName("levels");
            json.BeginArray();
            IEnumerable<Level> levels = new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => level.Elevation);
            foreach (Level level in levels)
            {
                // Level.Elevation is in feet, measured from the project's elevation base.
                double elevationFt = level.Elevation;
                double elevationM = RevitInternalUnits.FeetToMetres(elevationFt);
                json.BeginObject();
                json.Property("id", ElementIds.ValueOf(level.Id));
                json.Property("name", level.Name);
                json.Property("elevationFt", elevationFt);
                json.Property("elevationM", elevationM);
                json.EndObject();
            }
            json.EndArray();
        }

        private void WriteMaterials(JsonWriter json)
        {
            // Every material in the model, with its parameters: it shows whether a material carries the grade.
            json.PropertyName("materials");
            json.BeginArray();
            IEnumerable<Material> materials = new FilteredElementCollector(document)
                .OfClass(typeof(Material))
                .Cast<Material>();
            foreach (Material material in materials)
            {
                TryWriteBuffered(json, material.Id, buffer =>
                {
                    buffer.BeginObject();
                    buffer.Property("id", ElementIds.ValueOf(material.Id));
                    buffer.Property("name", material.Name);
                    buffer.Property("materialClass", material.MaterialClass);
                    WriteParameters(buffer, material);
                    buffer.EndObject();
                });
            }
            json.EndArray();
        }

        private int WriteElements(JsonWriter json)
        {
            json.PropertyName("elements");
            json.BeginArray();
            int writtenCount = 0;
            IEnumerable<Element> dumpedElements = new FilteredElementCollector(document)
                .WhereElementIsNotElementType()
                .Where(IsDumpedElement);
            foreach (Element element in dumpedElements)
            {
                bool isWritten = TryWriteBuffered(json, element.Id, buffer => WriteElement(buffer, element));
                if (isWritten)
                {
                    writtenCount++;
                    CountCategory(element);
                }
            }
            json.EndArray();
            return writtenCount;
        }

        /// <summary>
        /// Discovery, so the filter is loose: every element of a model category that is not drawn in one view
        /// only. Sketch lines and model lines are left out: every floor's boundary has them, and they carry no
        /// quantity. Linked models are not read: a collector on the host document sees only the host's elements.
        /// </summary>
        private static bool IsDumpedElement(Element element)
        {
            Category? category = element.Category;
            if (category == null)
            {
                return false;
            }
            bool isModelCategory = category.CategoryType == CategoryType.Model;
            bool isLineOrSketch = element is CurveElement || element is Sketch || element is SketchPlane;
            return isModelCategory && !element.ViewSpecific && !isLineOrSketch;
        }

        private void WriteElement(JsonWriter json, Element element)
        {
            ElementId typeId = element.GetTypeId();
            var elementType = document.GetElement(typeId) as ElementType;
            Element? level = document.GetElement(element.LevelId);

            json.BeginObject();
            json.Property("id", ElementIds.ValueOf(element.Id));
            json.Property("uniqueId", element.UniqueId);
            json.Property("class", element.GetType().Name);
            json.Property("category", element.Category.Name);
            json.Property("builtInCategory", element.Category.BuiltInCategory.ToString());
            json.Property("typeId", ElementIds.ValueOf(typeId));
            json.Property("familyName", elementType?.FamilyName);
            json.Property("typeName", elementType?.Name);
            json.Property("levelId", ElementIds.ValueOf(element.LevelId));
            json.Property("levelName", level?.Name);
            if (element is FamilyInstance familyInstance)
            {
                json.Property("structuralType", familyInstance.StructuralType.ToString());
            }
            WriteElementMaterials(json, element);
            WriteParameters(json, element);
            json.EndObject();

            if (elementType != null)
            {
                usedTypeIdsByValue[ElementIds.ValueOf(typeId)] = typeId;
            }
        }

        private void WriteElementMaterials(JsonWriter json, Element element)
        {
            // Materials of the element's own geometry; painted-on materials are left out (returnPaintMaterials: false).
            json.PropertyName("materials");
            json.BeginArray();
            foreach (ElementId materialId in element.GetMaterialIds(false))
            {
                var material = document.GetElement(materialId) as Material;
                double volumeFt3 = element.GetMaterialVolume(materialId);
                double volumeM3 = RevitInternalUnits.CubicFeetToCubicMetres(volumeFt3);
                double areaFt2 = element.GetMaterialArea(materialId, false);
                double areaM2 = RevitInternalUnits.SquareFeetToSquareMetres(areaFt2);
                json.BeginObject();
                json.Property("materialId", ElementIds.ValueOf(materialId));
                json.Property("materialName", material?.Name);
                json.Property("volumeFt3", volumeFt3);
                json.Property("volumeM3", volumeM3);
                json.Property("areaFt2", areaFt2);
                json.Property("areaM2", areaM2);
                json.EndObject();
            }
            json.EndArray();
        }

        private void WriteTypes(JsonWriter json)
        {
            // Each type used by a dumped element, written once with its type parameters.
            json.PropertyName("types");
            json.BeginArray();
            foreach (KeyValuePair<long, ElementId> typeIdEntry in usedTypeIdsByValue.OrderBy(pair => pair.Key))
            {
                var elementType = document.GetElement(typeIdEntry.Value) as ElementType;
                if (elementType == null)
                {
                    continue;
                }
                TryWriteBuffered(json, elementType.Id, buffer =>
                {
                    buffer.BeginObject();
                    buffer.Property("id", ElementIds.ValueOf(elementType.Id));
                    buffer.Property("uniqueId", elementType.UniqueId);
                    buffer.Property("class", elementType.GetType().Name);
                    buffer.Property("category", elementType.Category?.Name);
                    buffer.Property("familyName", elementType.FamilyName);
                    buffer.Property("typeName", elementType.Name);
                    WriteParameters(buffer, elementType);
                    buffer.EndObject();
                });
            }
            json.EndArray();
        }

        private void WriteParameters(JsonWriter json, Element element)
        {
            json.PropertyName("parameters");
            json.BeginArray();
            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter.Definition == null)
                {
                    continue;
                }
                long definitionId = ElementIds.ValueOf(parameter.Id);
                if (!firstParameterByDefinitionId.ContainsKey(definitionId))
                {
                    firstParameterByDefinitionId[definitionId] = parameter;
                }

                json.BeginObject();
                json.Property("definition", definitionId);
                json.PropertyName("value");
                WriteRawValue(json, parameter);
                json.Property("display", parameter.AsValueString());
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteRawValue(JsonWriter json, Parameter parameter)
        {
            if (!parameter.HasValue)
            {
                json.NullValue();
                return;
            }
            switch (parameter.StorageType)
            {
                case StorageType.Double:
                    json.NumberValue(parameter.AsDouble());
                    break;
                case StorageType.Integer:
                    json.NumberValue((long)parameter.AsInteger());
                    break;
                case StorageType.String:
                    json.StringValue(parameter.AsString());
                    break;
                case StorageType.ElementId:
                    json.NumberValue(ElementIds.ValueOf(parameter.AsElementId()));
                    break;
                default:
                    json.NullValue();
                    break;
            }
        }

        private void WriteParameterDefinitions(JsonWriter json)
        {
            json.PropertyName("parameterDefinitions");
            json.BeginArray();
            foreach (KeyValuePair<long, Parameter> entry in firstParameterByDefinitionId.OrderBy(pair => pair.Key))
            {
                Parameter parameter = entry.Value;
                TryWriteBuffered(json, parameter.Id, buffer => WriteParameterDefinition(buffer, entry.Key, parameter));
            }
            json.EndArray();
        }

        private void WriteParameterDefinition(JsonWriter json, long definitionId, Parameter parameter)
        {
            Definition definition = parameter.Definition;
            var internalDefinition = definition as InternalDefinition;
            bool isBuiltIn = internalDefinition != null && internalDefinition.BuiltInParameter != BuiltInParameter.INVALID;
            string? builtInParameterName = isBuiltIn ? internalDefinition!.BuiltInParameter.ToString() : null;
            // A shared parameter's GUID stays the same in every project and Revit year (F-002 asks about it).
            var sharedParameter = document.GetElement(parameter.Id) as SharedParameterElement;

            json.BeginObject();
            json.Property("id", definitionId);
            json.Property("name", definition.Name);
            json.Property("builtInParameter", builtInParameterName);
            json.Property("isShared", sharedParameter != null);
            json.Property("sharedGuid", sharedParameter?.GuidValue.ToString());
            json.Property("dataType", definition.GetDataType().TypeId);
            json.Property("group", definition.GetGroupTypeId().TypeId);
            json.Property("storageType", parameter.StorageType.ToString());
            json.EndObject();
        }

        private void WriteUnreadable(JsonWriter json)
        {
            json.PropertyName("unreadable");
            json.BeginArray();
            foreach (KeyValuePair<long, string> unreadable in unreadableIdsWithErrors)
            {
                json.BeginObject();
                json.Property("id", unreadable.Key);
                json.Property("error", unreadable.Value);
                json.EndObject();
            }
            json.EndArray();
        }

        private void WriteSummary(JsonWriter json, int elementCount, double readTimeSeconds)
        {
            json.PropertyName("summary");
            json.BeginObject();
            json.Property("elementCount", elementCount);
            json.Property("unreadableCount", unreadableIdsWithErrors.Count);
            json.Property("typeCount", usedTypeIdsByValue.Count);
            json.Property("parameterDefinitionCount", firstParameterByDefinitionId.Count);
            json.Property("readTimeSeconds", readTimeSeconds);
            json.PropertyName("elementCountByCategory");
            json.BeginObject();
            foreach (KeyValuePair<string, int> categoryCount in elementCountByCategory)
            {
                json.Property(categoryCount.Key, categoryCount.Value);
            }
            json.EndObject();
            json.EndObject();
        }

        private void CountCategory(Element element)
        {
            string categoryName = element.Category.Name;
            elementCountByCategory.TryGetValue(categoryName, out int countSoFar);
            elementCountByCategory[categoryName] = countSoFar + 1;
        }

        /// <summary>
        /// Writes one value into a buffer first, so anything Revit fails to read is listed under "unreadable"
        /// (with its id and the error) and the rest of the file stays valid JSON. Returns whether it was written.
        /// </summary>
        private bool TryWriteBuffered(JsonWriter json, ElementId id, Action<JsonWriter> writeValue)
        {
            var buffer = new StringWriter();
            try
            {
                writeValue(new JsonWriter(buffer));
            }
            catch (Exception exception)
            {
                string error = exception.GetType().Name + ": " + exception.Message;
                unreadableIdsWithErrors.Add(new KeyValuePair<long, string>(ElementIds.ValueOf(id), error));
                return false;
            }
            json.RawValue(buffer.ToString());
            return true;
        }
    }
}
