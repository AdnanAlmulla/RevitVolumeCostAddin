using Addin.Standard;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Addin.Commands
{
    /// <summary>
    /// The "Parameter standard" button: shows the shared parameters the technicians fill in, their allowed values and the
    /// rules (ParameterStandardWindow). Reads nothing from the model, so it works with no model open.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class ParameterStandardCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var window = new ParameterStandardWindow(AddinInfo.ProductName, commandData.Application.MainWindowHandle);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
