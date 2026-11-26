using System.IO;
using Addin.Json;
using Addin.Shapes;
using Xunit;

namespace Addin.Tests
{
    public class ShapeMeshTests
    {
        // A made-up 1 m cube with one corner at (2 m, 3 m, 0 m): 6 faces, each split into 2 triangles.
        private static ShapeMesh MadeUpCube()
        {
            var mesh = new ShapeMesh();
            double[] originM = { 2.0, 3.0, 0.0 };
            PointM Corner(int xStep, int yStep, int zStep) => new PointM(originM[0] + xStep, originM[1] + yStep, originM[2] + zStep);
            int[][] faces =
            {
                new[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0 }, // bottom
                new[] { 0, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1 }, // top
                new[] { 0, 0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 1 }, // south
                new[] { 0, 1, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1 }, // north
                new[] { 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1 }, // west
                new[] { 1, 0, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1 }, // east
            };
            foreach (int[] face in faces)
            {
                PointM first = Corner(face[0], face[1], face[2]);
                PointM second = Corner(face[3], face[4], face[5]);
                PointM third = Corner(face[6], face[7], face[8]);
                PointM fourth = Corner(face[9], face[10], face[11]);
                mesh.AddTriangle(first, second, third);
                mesh.AddTriangle(first, third, fourth);
            }
            return mesh;
        }

        [Fact]
        public void A_cube_keeps_its_12_triangles_and_shares_its_8_corners()
        {
            ShapeMesh cube = MadeUpCube();

            Assert.Equal(12, cube.TriangleCount);
            Assert.Equal(8, cube.CornerCount);
        }

        [Fact]
        public void Corners_are_written_in_whole_millimetres()
        {
            ShapeMesh cube = MadeUpCube();

            // The first corner is (2 m, 3 m, 0 m) = (2000 mm, 3000 mm, 0 mm).
            Assert.Equal(new long[] { 2000, 3000, 0 }, cube.PositionsMm.GetRange(0, 3).ToArray());
        }

        [Fact]
        public void Corners_less_than_half_a_millimetre_apart_are_one_corner()
        {
            var mesh = new ShapeMesh();
            var corner = new PointM(1.0, 1.0, 1.0);
            var cornerOffByAThirdOfAMillimetre = new PointM(1.0003, 1.0, 1.0);

            mesh.AddTriangle(corner, new PointM(2.0, 1.0, 1.0), new PointM(1.0, 2.0, 1.0));
            mesh.AddTriangle(cornerOffByAThirdOfAMillimetre, new PointM(1.0, 2.0, 1.0), new PointM(1.0, 1.0, 2.0));

            Assert.Equal(4, mesh.CornerCount);
        }

        [Fact]
        public void A_triangle_flattened_by_rounding_is_dropped()
        {
            var mesh = new ShapeMesh();

            mesh.AddTriangle(new PointM(0, 0, 0), new PointM(0.0002, 0, 0), new PointM(1, 1, 0));

            Assert.Equal(0, mesh.TriangleCount);
        }

        [Fact]
        public void An_element_is_written_with_its_ids_corners_and_triangles()
        {
            var mesh = new ShapeMesh();
            mesh.AddTriangle(new PointM(0, 0, 0), new PointM(1, 0, 0), new PointM(0, 1, 0));
            var text = new StringWriter();

            ShapeFile.WriteElement(new JsonWriter(text), 12345, "made-up-unique-id", "Structural Columns", mesh);

            Assert.Equal(
                "{\"elementId\":12345,\"uniqueId\":\"made-up-unique-id\",\"category\":\"Structural Columns\"," +
                "\"positionsMm\":[0,0,0,1000,0,0,0,1000,0],\"triangles\":[0,1,2]}",
                text.ToString());
        }
    }
}
