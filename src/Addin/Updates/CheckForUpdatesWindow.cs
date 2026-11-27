using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Addin.Updates
{
    /// <summary>
    /// Help's "Check for updates" (0.7.2): asks the uploader to check the website now (UpdateCheckFiles: the add-in never goes
    /// on the network) and shows its answer in plain words. Waits without freezing Revit: the window keeps Revit's
    /// messages moving and looks for the answer twice a second, for at most a minute.
    /// </summary>
    internal sealed class CheckForUpdatesWindow : Window
    {
        private static readonly TimeSpan TIME_BETWEEN_LOOKS = TimeSpan.FromMilliseconds(500);
        // A check is two small requests, plus a download of about 0.25 MB when there is a new version.
        private static readonly TimeSpan LONGEST_WAIT = TimeSpan.FromMinutes(1);

        private readonly Guid requestId = Guid.NewGuid();
        private readonly string resultPath;
        private readonly DateTime giveUpAtUtc;
        private readonly DispatcherTimer lookTimer;
        private readonly TextBlock statusText;
        private readonly Button closeButton;

        public CheckForUpdatesWindow(IntPtr revitWindowHandle)
        {
            string localProductFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AddinInfo.ProductName);
            resultPath = Path.Combine(localProductFolder, UpdateCheckFiles.RESULT_FILE_NAME);

            Title = AddinInfo.ProductName + ": check for updates";
            Width = 460;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 12;
            Background = Brushes.White;
            new WindowInteropHelper(this).Owner = revitWindowHandle;

            statusText = new TextBlock { Text = "Checking for updates…", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24, 24, 24, 12) };
            closeButton = new Button { Content = "Close", IsCancel = true, IsDefault = true, MinWidth = 88, Padding = new Thickness(12, 4, 12, 4), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 0, 24, 16) };
            closeButton.Click += (sender, arguments) => Close();
            var layout = new StackPanel();
            layout.Children.Add(statusText);
            layout.Children.Add(closeButton);
            Content = layout;

            Directory.CreateDirectory(localProductFolder);
            WriteRequest(Path.Combine(localProductFolder, UpdateCheckFiles.REQUEST_FILE_NAME));
            giveUpAtUtc = DateTime.UtcNow + LONGEST_WAIT;
            lookTimer = new DispatcherTimer { Interval = TIME_BETWEEN_LOOKS };
            lookTimer.Tick += (sender, arguments) => LookForAnswer();
            lookTimer.Start();
            Closed += (sender, arguments) => lookTimer.Stop();
        }

        /// <summary>The request, written whole under a temporary name, so the uploader never reads half of it.</summary>
        private void WriteRequest(string requestPath)
        {
            string temporaryPath = requestPath + ".partial";
            File.WriteAllText(temporaryPath, requestId.ToString("D"));
            if (File.Exists(requestPath))
            {
                File.Delete(requestPath);
            }
            File.Move(temporaryPath, requestPath);
        }

        private void LookForAnswer()
        {
            string? resultText = null;
            try
            {
                resultText = File.Exists(resultPath) ? File.ReadAllText(resultPath) : null;
            }
            catch (IOException)
            {
                // Being written: looked at again in half a second.
            }
            if (UpdateCheckFiles.TryReadResult(resultText, requestId, out UpdateCheckOutcome outcome, out string message))
            {
                lookTimer.Stop();
                ShowAnswer(outcome, message);
                return;
            }
            if (DateTime.UtcNow >= giveUpAtUtc)
            {
                lookTimer.Stop();
                statusText.Text = "The uploader did not answer within a minute. In Task Manager > Details, check that " +
                    AddinInfo.ProductName + ".Uploader.exe is running, or sign out of Windows and back in.";
                AddinInfo.Log("Check for updates: no answer from the uploader within a minute");
            }
        }

        private void ShowAnswer(UpdateCheckOutcome outcome, string message)
        {
            AddinInfo.Log("Check for updates: " + outcome + ", " + message);
            if (outcome == UpdateCheckOutcome.Installed || UpdateNotice.IsNewerVersionInstalled())
            {
                statusText.Text = "New version of " + AddinInfo.ProductName + " Addin ready, restart Revit please.\n\n" + message;
                statusText.FontWeight = FontWeights.SemiBold;
                // Said here, so the note while Revit is idle does not say it again this session.
                UpdateNotice.MarkShown();
                return;
            }
            statusText.Text = message;
        }
    }
}
