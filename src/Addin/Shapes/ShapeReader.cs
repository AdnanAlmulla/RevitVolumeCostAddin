using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Addin.Json;
using Addin.RevitApi;
using Addin.Units;
using Autodesk.Revit.DB;

namespace Addin.Shapes
{
    /// <summary>One way of asking Revit for shapes: how much detail, and how finely curved faces are cut into triangles.</summary>
    internal sealed class ShapeSetting
    {
        public ShapeSetting(string name, ViewDetailLevel detailLevel, double triangulationDetail)
        {
            Name = name;
            DetailLevel = detailLevel;
            TriangulationDetail = triangulationDetail;
        }

        public string Name { get; }

        public ViewDetailLevel DetailLevel { get; }

        /// <summary>Revit's Face.Triangulate level of detail: 0 is the coarsest, 1 the finest.</summary>
        public double TriangulationDetail { get; }
    }

    /// <summary>What one pass over the elements cost and gave (backlog F-003: the timing probe).</summary>
    internal sealed class ShapePassOutcome
    {
        public ShapePassOutcome(ShapeSetting setting)
        {
            Setting = setting;
        }

        public ShapeSetting Setting { get; }
        public int ElementCount { get; set; }
        public int ElementsWithoutShapeCount { get; set; }
        public int UnreadableCount { get; set; }
        public long TriangleCount { get; set; }
        public long CornerCount { get; set; }
        // Revit's thread is held for the first two: asking for the geometry, and cutting its faces into triangles.
        public double GeometrySeconds { get; set; }
        public double TriangulateSeconds { get; set; }
        // Writing the file could be done off Revit's thread later; here it runs inline, so it is timed apart.
        public double WriteSeconds { get; set; }
        public double TotalSeconds { get; set; }
        public SortedDictionary<string, CategoryShapeCount> CountByCategory { get; } = new SortedDictionary<string, CategoryShapeCount>();
    }

    /// <summary>
    /// Reads each element's shape as triangles in metres, keyed by Revit element id (backlog F-003). Only reads, inside
    /// Revit's API context. An element Revit fails to read is counted, not fatal.
    /// </summary>
    internal static class ShapeReader
    {
        private const int PROGRESS_LOG_EVERY_ELEMENTS = 1000;

        /// <summary>One pass over the elements. With elementsJson, each element's shape is written as it is read.</summary>
        public static ShapePassOutcome Run(IReadOnlyList<Element> elements, ShapeSetting setting, JsonWriter? elementsJson)
        {
            var outcome = new ShapePassOutcome(setting);
            var options = new Options
            {
                DetailLevel = setting.DetailLevel,
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
            };
            Stopwatch totalTime = Stopwatch.StartNew();
            var geometryTime = new Stopwatch();
            var triangulateTime = new Stopwatch();
            var writeTime = new Stopwatch();

            foreach (Element element in elements)
            {
                outcome.ElementCount++;
                if (outcome.ElementCount % PROGRESS_LOG_EVERY_ELEMENTS == 0)
                {
                    AddinInfo.Log($"3D shapes ({setting.Name}): {outcome.ElementCount} of {elements.Count} elements, {totalTime.Elapsed.TotalSeconds:0.0} s so far");
                }
                try
                {
                    geometryTime.Start();
                    var solids = new List<Solid>();
                    var meshes = new List<Mesh>();
                    CollectSolidsAndMeshes(element.get_Geometry(options), solids, meshes);
                    geometryTime.Stop();

                    triangulateTime.Start();
                    var shape = new ShapeMesh();
                    foreach (Solid solid in solids)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            AddTriangles(shape, face.Triangulate(setting.TriangulationDetail));
                        }
                    }
                    foreach (Mesh mesh in meshes)
                    {
                        AddTriangles(shape, mesh);
                    }
                    triangulateTime.Stop();

                    string categoryName = element.Category?.Name ?? "(no category)";
                    Count(outcome, categoryName, shape);

                    if (elementsJson != null)
                    {
                        writeTime.Start();
                        ShapeFile.WriteElement(elementsJson, ElementIds.ValueOf(element.Id), element.UniqueId, categoryName, shape);
                        writeTime.Stop();
                    }
                }
                catch (Exception)
                {
                    geometryTime.Stop();
                    triangulateTime.Stop();
                    writeTime.Stop();
                    outcome.UnreadableCount++;
                }
            }

            totalTime.Stop();
            outcome.GeometrySeconds = geometryTime.Elapsed.TotalSeconds;
            outcome.TriangulateSeconds = triangulateTime.Elapsed.TotalSeconds;
            outcome.WriteSeconds = writeTime.Elapsed.TotalSeconds;
            outcome.TotalSeconds = totalTime.Elapsed.TotalSeconds;
            return outcome;
        }

        public static void Log(ShapePassOutcome outcome)
        {
            AddinInfo.Log(string.Format(CultureInfo.InvariantCulture,
                "3D shapes ({0}): {1} elements, {2} without a shape, {3} unreadable, {4:N0} triangles, {5:N0} corners; " +
                "geometry {6:0.0} s, triangles {7:0.0} s, writing {8:0.0} s, total {9:0.0} s",
                outcome.Setting.Name, outcome.ElementCount, outcome.ElementsWithoutShapeCount, outcome.UnreadableCount,
                outcome.TriangleCount, outcome.CornerCount,
                outcome.GeometrySeconds, outcome.TriangulateSeconds, outcome.WriteSeconds, outcome.TotalSeconds));
            foreach (KeyValuePair<string, CategoryShapeCount> entry in outcome.CountByCategory)
            {
                AddinInfo.Log(string.Format(CultureInfo.InvariantCulture,
                    "3D shapes ({0}):   {1}: {2} elements, {3} without a shape, {4:N0} triangles",
                    outcome.Setting.Name, entry.Key, entry.Value.ElementCount, entry.Value.ElementsWithoutShapeCount, entry.Value.TriangleCount));
            }
        }

        public static void WriteSummary(JsonWriter json, ShapePassOutcome outcome)
        {
            json.BeginObject();
            json.Property("setting", outcome.Setting.Name);
            json.Property("detailLevel", outcome.Setting.DetailLevel.ToString());
            json.Property("triangulationDetail", outcome.Setting.TriangulationDetail);
            json.Property("elements", outcome.ElementCount);
            json.Property("elementsWithoutShape", outcome.ElementsWithoutShapeCount);
            json.Property("unreadable", outcome.UnreadableCount);
            json.Property("triangles", outcome.TriangleCount);
            json.Property("corners", outcome.CornerCount);
            json.Property("geometrySeconds", outcome.GeometrySeconds);
            json.Property("triangulateSeconds", outcome.TriangulateSeconds);
            json.Property("writeSeconds", outcome.WriteSeconds);
            json.Property("totalSeconds", outcome.TotalSeconds);
            json.PropertyName("categories");
            ShapeFile.WriteCategoryCounts(json, outcome.CountByCategory);
            json.EndObject();
        }

        private static void Count(ShapePassOutcome outcome, string categoryName, ShapeMesh shape)
        {
            if (!outcome.CountByCategory.TryGetValue(categoryName, out CategoryShapeCount? categoryCount))
            {
                categoryCount = new CategoryShapeCount();
                outcome.CountByCategory.Add(categoryName, categoryCount);
            }
            bool hasNoShape = shape.TriangleCount == 0;
            categoryCount.ElementCount++;
            categoryCount.TriangleCount += shape.TriangleCount;
            categoryCount.CornerCount += shape.CornerCount;
            outcome.TriangleCount += shape.TriangleCount;
            outcome.CornerCount += shape.CornerCount;
            if (hasNoShape)
            {
                categoryCount.ElementsWithoutShapeCount++;
                outcome.ElementsWithoutShapeCount++;
            }
        }

        /// <summary>
        /// Solids with a volume and loose meshes, at any depth. A family instance's geometry sits inside a
        /// GeometryInstance; GetInstanceGeometry returns it already placed in the model's coordinates.
        /// </summary>
        private static void CollectSolidsAndMeshes(GeometryElement? geometry, List<Solid> solids, List<Mesh> meshes)
        {
            if (geometry == null)
            {
                return;
            }
            foreach (GeometryObject geometryObject in geometry)
            {
                switch (geometryObject)
                {
                    case Solid solid when solid.Faces.Size > 0 && solid.Volume > 0:
                        solids.Add(solid);
                        break;
                    case Mesh mesh:
                        meshes.Add(mesh);
                        break;
                    case GeometryInstance instance:
                        CollectSolidsAndMeshes(instance.GetInstanceGeometry(), solids, meshes);
                        break;
                }
            }
        }

        private static void AddTriangles(ShapeMesh shape, Mesh mesh)
        {
            for (int triangleIndex = 0; triangleIndex < mesh.NumTriangles; triangleIndex++)
            {
                MeshTriangle triangle = mesh.get_Triangle(triangleIndex);
                shape.AddTriangle(ToMetres(triangle.get_Vertex(0)), ToMetres(triangle.get_Vertex(1)), ToMetres(triangle.get_Vertex(2)));
            }
        }

        private static PointM ToMetres(XYZ pointFt)
        {
            return new PointM(
                RevitInternalUnits.FeetToMetres(pointFt.X),
                RevitInternalUnits.FeetToMetres(pointFt.Y),
                RevitInternalUnits.FeetToMetres(pointFt.Z));
        }
    }
}
