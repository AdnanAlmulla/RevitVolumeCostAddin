using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using Addin.Json;
using Xunit;

namespace Addin.Tests
{
    public class JsonWriterTests
    {
        private static string Write(System.Action<JsonWriter> write)
        {
            var output = new StringWriter();
            write(new JsonWriter(output));
            return output.ToString();
        }

        [Fact]
        public void Nested_objects_and_arrays_get_commas_only_between_items()
        {
            string json = Write(writer =>
            {
                writer.BeginObject();
                writer.Property("name", "Beam");
                writer.PropertyName("materials");
                writer.BeginArray();
                writer.BeginObject();
                writer.Property("volumeM3", 1.08);
                writer.EndObject();
                writer.BeginObject();
                writer.Property("volumeM3", 2.24);
                writer.EndObject();
                writer.EndArray();
                writer.Property("isShared", true);
                writer.PropertyName("empty");
                writer.BeginArray();
                writer.EndArray();
                writer.EndObject();
            });

            Assert.Equal("{\"name\":\"Beam\",\"materials\":[{\"volumeM3\":1.08},{\"volumeM3\":2.24}],\"isShared\":true,\"empty\":[]}", json);
        }

        [Fact]
        public void Text_with_quotes_backslashes_and_control_characters_reads_back_unchanged()
        {
            string awkwardText = "C35/45 \"PT\" slab\\B1\r\n\ttab\u0001 Ø600 é";

            string json = Write(writer =>
            {
                writer.BeginObject();
                writer.Property("text", awkwardText);
                writer.EndObject();
            });
            string readBack = JsonDocument.Parse(json).RootElement.GetProperty("text").GetString()!;

            Assert.Equal(awkwardText, readBack);
        }

        [Fact]
        public void Numbers_use_a_decimal_point_whatever_the_regional_settings()
        {
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                // German settings write 1.5 as "1,5", which would break the JSON.
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                string json = Write(writer =>
                {
                    writer.BeginArray();
                    writer.NumberValue(1.5);
                    writer.NumberValue(1234567.25);
                    writer.EndArray();
                });

                Assert.Equal("[1.5,1234567.25]", json);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Fact]
        public void A_volume_reads_back_to_the_same_double()
        {
            double volumeFt3 = 38.13986688903313;

            string json = Write(writer => writer.NumberValue(volumeFt3));
            double readBack = JsonDocument.Parse(json).RootElement.GetDouble();

            Assert.Equal(volumeFt3, readBack);
        }

        [Fact]
        public void Not_a_number_and_null_text_are_written_as_null()
        {
            string json = Write(writer =>
            {
                writer.BeginArray();
                writer.NumberValue(double.NaN);
                writer.NumberValue(double.PositiveInfinity);
                writer.StringValue(null);
                writer.EndArray();
            });

            Assert.Equal("[null,null,null]", json);
        }

        [Fact]
        public void A_value_written_into_a_buffer_joins_the_array_with_a_comma()
        {
            var buffer = new StringWriter();
            var bufferWriter = new JsonWriter(buffer);
            bufferWriter.BeginObject();
            bufferWriter.Property("id", 42L);
            bufferWriter.EndObject();

            string json = Write(writer =>
            {
                writer.BeginArray();
                writer.RawValue(buffer.ToString());
                writer.RawValue(buffer.ToString());
                writer.EndArray();
            });

            Assert.Equal("[{\"id\":42},{\"id\":42}]", json);
        }
    }
}
