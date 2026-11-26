using Addin.Units;
using Xunit;

namespace Addin.Tests
{
    /// <summary>
    /// The ft³ to m³ conversion against hand-checked volumes (CLAUDE.md "Units").
    /// Element sizes are written in millimetres as an engineer would draw them, turned into feet the way Revit
    /// stores them (1 ft = 304.8 mm), and the converted result is checked against the hand calculation.
    /// </summary>
    public class RevitInternalUnitsTests
    {
        private const double MILLIMETRES_PER_FOOT = 304.8;

        // Far below any quantity a schedule shows (0.001 m³), far above floating-point rounding.
        private const double HAND_CALCULATION_TOLERANCE_M3 = 1e-9;
        private const double HAND_CALCULATION_TOLERANCE_M2 = 1e-9;

        private static double MillimetresToFeet(double lengthMm)
        {
            double lengthFt = lengthMm / MILLIMETRES_PER_FOOT;
            return lengthFt;
        }

        [Fact]
        public void Factors_are_powers_of_the_international_foot()
        {
            double footSquared = RevitInternalUnits.METRES_PER_FOOT * RevitInternalUnits.METRES_PER_FOOT;
            double footCubed = footSquared * RevitInternalUnits.METRES_PER_FOOT;

            Assert.Equal(RevitInternalUnits.SQUARE_METRES_PER_SQUARE_FOOT, footSquared, 15);
            Assert.Equal(RevitInternalUnits.CUBIC_METRES_PER_CUBIC_FOOT, footCubed, 15);
        }

        [Fact]
        public void Beam_300_by_600_by_6000_mm_is_1_08_m3()
        {
            // Hand calculation: 0.300 m × 0.600 m × 6.000 m = 1.080 m³.
            double widthFt = MillimetresToFeet(300);
            double depthFt = MillimetresToFeet(600);
            double lengthFt = MillimetresToFeet(6000);
            double volumeFt3 = widthFt * depthFt * lengthFt;

            double volumeM3 = RevitInternalUnits.CubicFeetToCubicMetres(volumeFt3);

            Assert.Equal(1.080, volumeM3, HAND_CALCULATION_TOLERANCE_M3);
        }

        [Fact]
        public void Column_800_by_800_by_3500_mm_is_2_24_m3()
        {
            // Hand calculation: 0.800 m × 0.800 m × 3.500 m = 2.240 m³.
            double widthFt = MillimetresToFeet(800);
            double depthFt = MillimetresToFeet(800);
            double heightFt = MillimetresToFeet(3500);
            double volumeFt3 = widthFt * depthFt * heightFt;

            double volumeM3 = RevitInternalUnits.CubicFeetToCubicMetres(volumeFt3);

            Assert.Equal(2.240, volumeM3, HAND_CALCULATION_TOLERANCE_M3);
        }

        [Fact]
        public void Slab_250_mm_thick_8_by_8_m_bay_is_16_m3()
        {
            // Hand calculation: 8.000 m × 8.000 m × 0.250 m = 16.000 m³.
            double lengthFt = MillimetresToFeet(8000);
            double widthFt = MillimetresToFeet(8000);
            double thicknessFt = MillimetresToFeet(250);
            double volumeFt3 = lengthFt * widthFt * thicknessFt;

            double volumeM3 = RevitInternalUnits.CubicFeetToCubicMetres(volumeFt3);

            Assert.Equal(16.000, volumeM3, HAND_CALCULATION_TOLERANCE_M3);
        }

        [Fact]
        public void One_cubic_yard_is_0_764554857984_m3()
        {
            // 1 yd³ = 27 ft³ = 27 × 0.028316846592 m³ = 0.764554857984 m³ exactly.
            double volumeFt3 = 27;

            double volumeM3 = RevitInternalUnits.CubicFeetToCubicMetres(volumeFt3);

            Assert.Equal(0.764554857984, volumeM3, HAND_CALCULATION_TOLERANCE_M3);
        }

        [Fact]
        public void Slab_face_8_by_8_m_is_64_m2()
        {
            // Hand calculation: 8.000 m × 8.000 m = 64.000 m².
            double lengthFt = MillimetresToFeet(8000);
            double widthFt = MillimetresToFeet(8000);
            double areaFt2 = lengthFt * widthFt;

            double areaM2 = RevitInternalUnits.SquareFeetToSquareMetres(areaFt2);

            Assert.Equal(64.000, areaM2, HAND_CALCULATION_TOLERANCE_M2);
        }

        [Fact]
        public void One_acre_is_4046_8564224_m2()
        {
            // 1 acre = 43,560 ft² = 43,560 × 0.09290304 m² = 4,046.8564224 m² exactly.
            double areaFt2 = 43560;

            double areaM2 = RevitInternalUnits.SquareFeetToSquareMetres(areaFt2);

            Assert.Equal(4046.8564224, areaM2, HAND_CALCULATION_TOLERANCE_M2);
        }

        [Fact]
        public void Level_at_3600_mm_is_3_6_m()
        {
            double elevationFt = MillimetresToFeet(3600);

            double elevationM = RevitInternalUnits.FeetToMetres(elevationFt);

            Assert.Equal(3.600, elevationM, 12);
        }
    }
}
