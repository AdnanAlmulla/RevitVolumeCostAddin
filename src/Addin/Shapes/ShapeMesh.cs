// Pure code (no Revit API): compiled into tests/Addin.Tests by path.
using System;
using System.Collections.Generic;

namespace Addin.Shapes
{
    /// <summary>A point in metres, in the model's coordinates (Revit's internal origin; X east, Y north, Z up).</summary>
    public readonly struct PointM
    {
        public PointM(double xM, double yM, double zM)
        {
            XM = xM;
            YM = yM;
            ZM = zM;
        }

        public double XM { get; }
        public double YM { get; }
        public double ZM { get; }
    }

    /// <summary>
    /// One element's shape for the website's 3D view: triangles whose corners are rounded to the nearest millimetre and
    /// shared, so each corner is written once. A triangle that rounding flattens (two corners in the same millimetre)
    /// is dropped: it would draw nothing.
    /// </summary>
    public sealed class ShapeMesh
    {
        // Positions are written as whole millimetres: finer than any structural drawing needs, and short in the file.
        public const double MILLIMETRES_PER_METRE = 1000.0;

        private readonly Dictionary<(long XMm, long YMm, long ZMm), int> cornerIndexByPosition = new Dictionary<(long, long, long), int>();

        /// <summary>x, y, z of each corner in turn, in millimetres.</summary>
        public List<long> PositionsMm { get; } = new List<long>();

        /// <summary>Three corner numbers per triangle (counting corners from 0).</summary>
        public List<int> CornerIndices { get; } = new List<int>();

        public int CornerCount => PositionsMm.Count / 3;

        public int TriangleCount => CornerIndices.Count / 3;

        public void AddTriangle(PointM firstCorner, PointM secondCorner, PointM thirdCorner)
        {
            int firstIndex = IndexOf(firstCorner);
            int secondIndex = IndexOf(secondCorner);
            int thirdIndex = IndexOf(thirdCorner);
            bool isFlattened = firstIndex == secondIndex || secondIndex == thirdIndex || thirdIndex == firstIndex;
            if (isFlattened)
            {
                return;
            }
            CornerIndices.Add(firstIndex);
            CornerIndices.Add(secondIndex);
            CornerIndices.Add(thirdIndex);
        }

        private int IndexOf(PointM corner)
        {
            long xMm = RoundToMillimetres(corner.XM);
            long yMm = RoundToMillimetres(corner.YM);
            long zMm = RoundToMillimetres(corner.ZM);
            (long, long, long) position = (xMm, yMm, zMm);
            if (cornerIndexByPosition.TryGetValue(position, out int existingIndex))
            {
                return existingIndex;
            }
            int newIndex = CornerCount;
            cornerIndexByPosition.Add(position, newIndex);
            PositionsMm.Add(xMm);
            PositionsMm.Add(yMm);
            PositionsMm.Add(zMm);
            return newIndex;
        }

        private static long RoundToMillimetres(double lengthM)
        {
            double lengthMm = lengthM * MILLIMETRES_PER_METRE;
            return (long)Math.Round(lengthMm, MidpointRounding.AwayFromZero);
        }
    }
}
