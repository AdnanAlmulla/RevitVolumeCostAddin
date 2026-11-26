using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Addin.RevitApi;
using Addin.Units;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Contracts;

namespace Addin.Snapshots
{
    /// <summary>
    /// Reads the open model into a snapshot: every model element with a material volume, its materials, levels,
    /// base and top, and every project and shared parameter with a value (facts only; the website classifies).
    /// Only reads, inside Revit's API context. An element Revit fails to read is counted, not fatal.
    /// </summary>
    internal static class SnapshotReader
    {
        // Where an element keeps its level when its own LevelId is empty, in the order they are tried
        // (seen in the raw dump: beams use the reference level, walls the base constraint, columns and stairs the base level).
        private static readonly BuiltInParameter[] LEVEL_FALLBACKS =
        {
            BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
            BuiltInParameter.WALL_BASE_CONSTRAINT,
            BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,
            BuiltInParameter.STAIRS_BASE_LEVEL_PARAM,
        };

        private static readonly BuiltInParameter[] BASE_LEVEL_PARAMETERS =
        {
            BuiltInParameter.WALL_BASE_CONSTRAINT,
            BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,
            BuiltInParameter.STAIRS_BASE_LEVEL_PARAM,
        };

        private static readonly BuiltInParameter[] TOP_LEVEL_PARAMETERS =
        {
            BuiltInParameter.WALL_HEIGHT_TYPE,
            BuiltInParameter.FAMILY_TOP_LEVEL_PARAM,
            BuiltInParameter.STAIRS_TOP_LEVEL_PARAM,
        };

        public static Snapshot Read(Document document, Application application, string trigger, string addinVersion)
        {
            Stopwatch readTime = Stopwatch.StartNew();
            var snapshot = new Snapshot
            {
                SnapshotId = Guid.NewGuid(),
                TakenAtUtc = DateTime.UtcNow,
                SyncedBy = application.Username,
                Trigger = trigger,
                AddinVersion = addinVersion,
                RevitVersion = application.SubVersionNumber,
                RevitBuild = application.VersionBuild,
                Model = ModelIdentityOf(document),
            };

            foreach (Level level in new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>().OrderBy(level => level.Elevation))
            {
                snapshot.Levels.Add(new SnapshotLevel
                {
                    ElementId = ElementIds.ValueOf(level.Id),
                    Name = level.Name,
                    ElevationM = RevitInternalUnits.FeetToMetres(level.Elevation),
                    ProjectElevationM = RevitInternalUnits.FeetToMetres(level.ProjectElevation),
                });
            }

            var usedTypeIds = new Dictionary<long, ElementId>();
            IEnumerable<Element> modelElements = new FilteredElementCollector(document)
                .WhereElementIsNotElementType()
                .Where(IsModelElement);
            foreach (Element element in modelElements)
            {
                try
                {
                    ElementFacts? facts = ReadElement(document, element);
                    if (facts == null)
                    {
                        continue;
                    }
                    snapshot.Elements.Add(facts);
                    if (facts.TypeId.HasValue)
                    {
                        usedTypeIds[facts.TypeId.Value] = element.GetTypeId();
                    }
                }
                catch (Exception)
                {
                    snapshot.UnreadableElementCount++;
                }
            }

            foreach (KeyValuePair<long, ElementId> typeEntry in usedTypeIds)
            {
                if (document.GetElement(typeEntry.Value) is ElementType elementType)
                {
                    snapshot.Types.Add(new ElementTypeFacts
                    {
                        TypeId = typeEntry.Key,
                        FamilyName = elementType.FamilyName,
                        TypeName = elementType.Name,
                        Parameters = ProjectAndSharedParameters(elementType),
                    });
                }
            }

            readTime.Stop();
            snapshot.ReadSeconds = readTime.Elapsed.TotalSeconds;
            return snapshot;
        }

        private static ModelIdentity ModelIdentityOf(Document document)
        {
            var identity = new ModelIdentity
            {
                Title = document.Title,
                IsWorkshared = document.IsWorkshared,
                IsDetached = document.IsDetached,
                IsInCloud = document.IsModelInCloud,
            };
            if (document.IsModelInCloud)
            {
                ModelPath cloudModelPath = document.GetCloudModelPath();
                identity.AccProjectId = cloudModelPath.GetProjectGUID();
                identity.AccModelId = cloudModelPath.GetModelGUID();
                identity.CloudRegion = cloudModelPath.Region;
            }
            return identity;
        }

        /// <summary>Model elements that are not drawn in one view only, and not lines or sketches (as the raw dump).</summary>
        private static bool IsModelElement(Element element)
        {
            Category? category = element.Category;
            if (category == null || category.CategoryType != CategoryType.Model || element.ViewSpecific)
            {
                return false;
            }
            bool isLineOrSketch = element is CurveElement || element is Sketch || element is SketchPlane;
            return !isLineOrSketch;
        }

        /// <summary>The element's facts, or null when it has no material volume (nothing to quantify).</summary>
        private static ElementFacts? ReadElement(Document document, Element element)
        {
            var materials = new List<MaterialQuantity>();
            foreach (ElementId materialId in element.GetMaterialIds(false))
            {
                double volumeFt3 = element.GetMaterialVolume(materialId);
                if (volumeFt3 <= 0)
                {
                    continue;
                }
                var material = document.GetElement(materialId) as Material;
                materials.Add(new MaterialQuantity
                {
                    MaterialName = material?.Name ?? "",
                    MaterialClass = material?.MaterialClass ?? "",
                    VolumeM3 = RevitInternalUnits.CubicFeetToCubicMetres(volumeFt3),
                    AreaM2 = RevitInternalUnits.SquareFeetToSquareMetres(element.GetMaterialArea(materialId, false)),
                });
            }
            if (materials.Count == 0)
            {
                return null;
            }

            ElementId typeId = element.GetTypeId();
            var elementType = document.GetElement(typeId) as ElementType;
            BoundingBoxXYZ? boundingBox = element.get_BoundingBox(null);
            return new ElementFacts
            {
                UniqueId = element.UniqueId,
                ElementId = ElementIds.ValueOf(element.Id),
                Category = element.Category.Name,
                BuiltInCategory = element.Category.BuiltInCategory.ToString(),
                TypeId = elementType != null ? ElementIds.ValueOf(typeId) : (long?)null,
                FamilyName = elementType?.FamilyName ?? "",
                TypeName = elementType?.Name ?? "",
                LevelName = LevelNameOf(document, element),
                BaseLevelName = FirstLevelName(document, element, BASE_LEVEL_PARAMETERS),
                TopLevelName = FirstLevelName(document, element, TOP_LEVEL_PARAMETERS),
                BottomElevationM = boundingBox != null ? RevitInternalUnits.FeetToMetres(boundingBox.Min.Z) : (double?)null,
                TopElevationM = boundingBox != null ? RevitInternalUnits.FeetToMetres(boundingBox.Max.Z) : (double?)null,
                Materials = materials,
                Parameters = ProjectAndSharedParameters(element),
            };
        }

        private static string? LevelNameOf(Document document, Element element)
        {
            if (document.GetElement(element.LevelId) is Level ownLevel)
            {
                return ownLevel.Name;
            }
            return FirstLevelName(document, element, LEVEL_FALLBACKS);
        }

        private static string? FirstLevelName(Document document, Element element, BuiltInParameter[] levelParameters)
        {
            foreach (BuiltInParameter levelParameter in levelParameters)
            {
                Parameter? parameter = element.get_Parameter(levelParameter);
                if (parameter == null || parameter.StorageType != StorageType.ElementId)
                {
                    continue;
                }
                if (document.GetElement(parameter.AsElementId()) is Level level)
                {
                    return level.Name;
                }
            }
            return null;
        }

        /// <summary>Every project and shared parameter with a value; built-in parameters are left out.</summary>
        private static List<ParameterFact> ProjectAndSharedParameters(Element element)
        {
            var facts = new List<ParameterFact>();
            foreach (Parameter parameter in element.Parameters)
            {
                if (!(parameter.Definition is InternalDefinition definition) || definition.BuiltInParameter != BuiltInParameter.INVALID || !parameter.HasValue)
                {
                    continue;
                }
                var fact = new ParameterFact
                {
                    Name = definition.Name,
                    SharedGuid = parameter.IsShared ? parameter.GUID : (Guid?)null,
                    Display = parameter.AsValueString(),
                };
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        fact.Text = parameter.AsString();
                        if (string.IsNullOrEmpty(fact.Text))
                        {
                            continue;
                        }
                        break;
                    case StorageType.Integer:
                        fact.Number = parameter.AsInteger();
                        break;
                    case StorageType.Double:
                        SetNumber(fact, parameter.AsDouble(), definition.GetDataType());
                        break;
                    case StorageType.ElementId:
                        fact.Text = parameter.AsValueString();
                        break;
                }
                facts.Add(fact);
            }
            return facts;
        }

        /// <summary>Lengths, areas and volumes in metric, through the one conversion place; other numbers as Revit stores them.</summary>
        private static void SetNumber(ParameterFact fact, double internalValue, ForgeTypeId dataType)
        {
            // ForgeTypeId has no == operator (Revit 2024 and 2026 API): Equals compares the spec itself.
            if (dataType.Equals(SpecTypeId.Length))
            {
                fact.Number = RevitInternalUnits.FeetToMetres(internalValue);
                fact.NumberUnit = "m";
            }
            else if (dataType.Equals(SpecTypeId.Area))
            {
                fact.Number = RevitInternalUnits.SquareFeetToSquareMetres(internalValue);
                fact.NumberUnit = "m2";
            }
            else if (dataType.Equals(SpecTypeId.Volume))
            {
                fact.Number = RevitInternalUnits.CubicFeetToCubicMetres(internalValue);
                fact.NumberUnit = "m3";
            }
            else
            {
                fact.Number = internalValue;
            }
        }
    }
}
