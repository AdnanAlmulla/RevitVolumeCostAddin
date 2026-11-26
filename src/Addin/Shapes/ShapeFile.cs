// Pure code (no Revit API): compiled into tests/Addin.Tests by path.
using System.Collections.Generic;
using Addin.Json;

namespace Addin.Shapes
{
    /// <summary>How many elements and triangles one category gave, for the timing probe's report.</summary>
    public sealed class CategoryShapeCount
    {
        public int ElementCount { get; set; }
        public int ElementsWithoutShapeCount { get; set; }
        public long TriangleCount { get; set; }
        public long CornerCount { get; set; }
    }

    /// <summary>
    /// The shapes file, format "shapes-1" (provisional: it will change once the 3D view is built, so it is kept out of
    /// src/Contracts). Gzipped JSON: a header, then one entry per element as it is read, then a summary, so a run that
    /// stops part-way still leaves the elements read so far.
    ///   { "format": "shapes-1", ..., "elements": [ { "elementId", "uniqueId", "category",
    ///     "positionsMm": [x, y, z, ...], "triangles": [first, second, third, ...] } ], "summary": { ... } }
    /// </summary>
    public static class ShapeFile
    {
        public const string FORMAT = "shapes-1";
        public const string FILE_EXTENSION = ".shapes.json.gz";
        public const string AXES = "Revit internal coordinates in millimetres: X east, Y north, Z up (project north may differ)";

        public static void WriteElement(JsonWriter json, long elementId, string uniqueId, string category, ShapeMesh mesh)
        {
            json.BeginObject();
            json.Property("elementId", elementId);
            json.Property("uniqueId", uniqueId);
            json.Property("category", category);
            json.PropertyName("positionsMm");
            json.BeginArray();
            foreach (long coordinateMm in mesh.PositionsMm)
            {
                json.NumberValue(coordinateMm);
            }
            json.EndArray();
            json.PropertyName("triangles");
            json.BeginArray();
            foreach (int cornerIndex in mesh.CornerIndices)
            {
                json.NumberValue(cornerIndex);
            }
            json.EndArray();
            json.EndObject();
        }

        public static void WriteCategoryCounts(JsonWriter json, IDictionary<string, CategoryShapeCount> countByCategory)
        {
            json.BeginObject();
            foreach (KeyValuePair<string, CategoryShapeCount> entry in countByCategory)
            {
                json.PropertyName(entry.Key);
                json.BeginObject();
                json.Property("elements", entry.Value.ElementCount);
                json.Property("elementsWithoutShape", entry.Value.ElementsWithoutShapeCount);
                json.Property("triangles", entry.Value.TriangleCount);
                json.Property("corners", entry.Value.CornerCount);
                json.EndObject();
            }
            json.EndObject();
        }
    }
}
