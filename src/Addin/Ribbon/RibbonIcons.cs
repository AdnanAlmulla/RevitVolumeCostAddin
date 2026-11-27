using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace Addin.Ribbon
{
    /// <summary>
    /// The ribbon icons, embedded in the DLL as "Icons.&lt;name&gt;-&lt;theme&gt;-&lt;size&gt;.png" (src/Addin/Addin.csproj),
    /// drawn by tools/Draw-RibbonIcons.ps1 in one flat colour per Revit theme.
    /// </summary>
    internal static class RibbonIcons
    {
        // Revit's ribbon shows a large button at 32 x 32 px and a small one (and the Quick Access Toolbar) at 16 x 16 px.
        public const int LARGE_ICON_SIZE_PIXELS = 32;
        public const int SMALL_ICON_SIZE_PIXELS = 16;

        public static BitmapSource Load(string iconName, bool isDarkTheme, int sizePixels)
        {
            string themeName = isDarkTheme ? "dark" : "light";
            string resourceName = "Icons." + iconName + "-" + themeName + "-" + sizePixels + ".png";
            using (Stream? stream = typeof(RibbonIcons).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("The icon " + resourceName + " is not in the add-in.");
                }
                // OnLoad reads the whole image now, so the stream can close; Freeze lets Revit use it on any thread.
                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                BitmapFrame icon = decoder.Frames[0];
                icon.Freeze();
                return icon;
            }
        }
    }
}
