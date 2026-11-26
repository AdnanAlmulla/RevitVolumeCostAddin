using Autodesk.Revit.DB;

namespace Addin.RevitApi
{
    /// <summary>
    /// The one place that turns an ElementId into a number (CLAUDE.md: ElementId became 64-bit in Revit 2024).
    /// </summary>
    internal static class ElementIds
    {
        public static long ValueOf(ElementId elementId)
        {
#if REVIT2022 || REVIT2023
            // Revit 2022 and 2023: element ids are 32-bit.
            return elementId.IntegerValue;
#else
            return elementId.Value;
#endif
        }
    }
}
