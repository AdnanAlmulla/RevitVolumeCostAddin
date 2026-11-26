// Pure code (no Revit API): compiled into tests/Addin.Tests by path.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Addin.Json
{
    /// <summary>
    /// A small streaming JSON writer. The add-in carries no third-party files (CLAUDE.md), and Revit 2024's
    /// .NET Framework 4.8 has no built-in JSON writer, so this writes JSON text directly.
    /// Numbers are always written in the invariant culture ("1.5", never "1,5"), whatever the PC's region.
    /// A non-finite number (NaN, infinity) is written as null, because JSON has no way to write it.
    /// </summary>
    public sealed class JsonWriter
    {
        private readonly TextWriter output;

        // One entry per open object or array: whether something has already been written inside it,
        // so the writer knows when a comma is needed.
        private readonly Stack<bool> hasWrittenInsideContainer = new Stack<bool>();

        // True right after a property name, when the next value belongs to that name and needs no comma.
        private bool isAfterPropertyName;

        public JsonWriter(TextWriter output)
        {
            this.output = output;
        }

        public void BeginObject()
        {
            WriteSeparatorBeforeValue();
            output.Write('{');
            hasWrittenInsideContainer.Push(false);
        }

        public void EndObject()
        {
            hasWrittenInsideContainer.Pop();
            output.Write('}');
        }

        public void BeginArray()
        {
            WriteSeparatorBeforeValue();
            output.Write('[');
            hasWrittenInsideContainer.Push(false);
        }

        public void EndArray()
        {
            hasWrittenInsideContainer.Pop();
            output.Write(']');
        }

        public void PropertyName(string name)
        {
            WriteSeparatorBeforeValue();
            WriteQuotedText(name);
            output.Write(':');
            isAfterPropertyName = true;
        }

        public void StringValue(string? text)
        {
            if (text == null)
            {
                NullValue();
                return;
            }
            WriteSeparatorBeforeValue();
            WriteQuotedText(text);
        }

        public void NumberValue(double number)
        {
            bool isFinite = !double.IsNaN(number) && !double.IsInfinity(number);
            if (!isFinite)
            {
                NullValue();
                return;
            }
            WriteSeparatorBeforeValue();
            // "R" keeps every digit, so the number reads back exactly as Revit gave it.
            output.Write(number.ToString("R", CultureInfo.InvariantCulture));
        }

        public void NumberValue(long number)
        {
            WriteSeparatorBeforeValue();
            output.Write(number.ToString(CultureInfo.InvariantCulture));
        }

        public void BooleanValue(bool value)
        {
            WriteSeparatorBeforeValue();
            output.Write(value ? "true" : "false");
        }

        public void NullValue()
        {
            WriteSeparatorBeforeValue();
            output.Write("null");
        }

        /// <summary>Writes a value that is already JSON text, such as one written into a buffer by another JsonWriter.</summary>
        public void RawValue(string jsonText)
        {
            WriteSeparatorBeforeValue();
            output.Write(jsonText);
        }

        // Shorthands for a named value, one property per call.
        public void Property(string name, string? text)
        {
            PropertyName(name);
            StringValue(text);
        }

        public void Property(string name, double number)
        {
            PropertyName(name);
            NumberValue(number);
        }

        public void Property(string name, long number)
        {
            PropertyName(name);
            NumberValue(number);
        }

        public void Property(string name, bool value)
        {
            PropertyName(name);
            BooleanValue(value);
        }

        private void WriteSeparatorBeforeValue()
        {
            if (isAfterPropertyName)
            {
                isAfterPropertyName = false;
                return;
            }
            if (hasWrittenInsideContainer.Count == 0)
            {
                return;
            }
            bool isFirstInsideContainer = !hasWrittenInsideContainer.Peek();
            if (!isFirstInsideContainer)
            {
                output.Write(',');
            }
            hasWrittenInsideContainer.Pop();
            hasWrittenInsideContainer.Push(true);
        }

        private void WriteQuotedText(string text)
        {
            output.Write('"');
            output.Write(EscapeText(text));
            output.Write('"');
        }

        /// <summary>Escapes text for a JSON string, as RFC 8259 section 7 requires.</summary>
        public static string EscapeText(string text)
        {
            var escaped = new StringBuilder(text.Length + 8);
            foreach (char character in text)
            {
                switch (character)
                {
                    case '"': escaped.Append("\\\""); break;
                    case '\\': escaped.Append("\\\\"); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    case '\b': escaped.Append("\\b"); break;
                    case '\f': escaped.Append("\\f"); break;
                    default:
                        // RFC 8259 section 7: control characters below U+0020 must be escaped.
                        bool isControlCharacter = character < ' ';
                        if (isControlCharacter)
                        {
                            escaped.Append("\\u");
                            escaped.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            escaped.Append(character);
                        }
                        break;
                }
            }
            return escaped.ToString();
        }
    }
}
