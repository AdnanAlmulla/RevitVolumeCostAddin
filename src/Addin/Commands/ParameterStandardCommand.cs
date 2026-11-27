using System;
using System.IO;
using Addin.Standard;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Addin.Commands
{
    /// <summary>
    /// The "Parameter standard" button: shows the shared parameters the technicians fill in, their allowed values and the
    /// rules (ParameterStandardWindow): the website's copy when the uploader has saved one, otherwise the copy built into the
    /// add-in. Reads nothing from the model, so it works with no model open.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class ParameterStandardCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            StandardContent standard = WebsiteCopyOrBuiltIn();
            var window = new ParameterStandardWindow(AddinInfo.ProductName, standard, commandData.Application.MainWindowHandle);
            window.ShowDialog();
            return Result.Succeeded;
        }

        /// <summary>The website's copy saved by the uploader when there is a complete one, otherwise the built-in standard.</summary>
        private static StandardContent WebsiteCopyOrBuiltIn()
        {
            try
            {
                string websiteCopyPath = ParameterStandardFile.PathFor(AddinInfo.ProductName);
                string? websiteCopyText = File.Exists(websiteCopyPath) ? File.ReadAllText(websiteCopyPath) : null;
                return ParameterStandardFile.Read(websiteCopyText) ?? ParameterStandard.BuiltIn;
            }
            catch (Exception exception)
            {
                AddinInfo.Log("Parameter standard: the website's copy could not be read, " + exception.GetType().Name + ": " + exception.Message);
                return ParameterStandard.BuiltIn;
            }
        }
    }
}
