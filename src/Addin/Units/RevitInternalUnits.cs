// Pure code (no Revit API): compiled into tests/Addin.Tests by path.
namespace Addin.Units
{
    /// <summary>
    /// The one place where Revit's internal units become metric (CLAUDE.md "Units").
    /// Revit stores every length in feet, every area in square feet and every volume in cubic feet,
    /// whatever units the project displays.
    /// </summary>
    public static class RevitInternalUnits
    {
        // The international foot is defined as exactly 0.3048 m (International Yard and Pound Agreement, 1959).
        public const double METRES_PER_FOOT = 0.3048;

        // 0.3048² = 0.09290304 exactly.
        public const double SQUARE_METRES_PER_SQUARE_FOOT = 0.09290304;

        // 0.3048³ = 0.028316846592 exactly.
        public const double CUBIC_METRES_PER_CUBIC_FOOT = 0.028316846592;

        public static double FeetToMetres(double lengthFt)
        {
            double lengthM = lengthFt * METRES_PER_FOOT;
            return lengthM;
        }

        public static double SquareFeetToSquareMetres(double areaFt2)
        {
            double areaM2 = areaFt2 * SQUARE_METRES_PER_SQUARE_FOOT;
            return areaM2;
        }

        public static double CubicFeetToCubicMetres(double volumeFt3)
        {
            double volumeM3 = volumeFt3 * CUBIC_METRES_PER_CUBIC_FOOT;
            return volumeM3;
        }
    }
}
