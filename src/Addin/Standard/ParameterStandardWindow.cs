using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Addin.Standard
{
    /// <summary>
    /// Shows the parameter standard (the website's copy or the built-in one, StandardContent) the way the office's Excel sheet does: one row per shared parameter
    /// with its allowed values, categories, which elements to fill and what a blank means, then the settings, rules and
    /// examples. Built in code (no XAML), read-only. Names and values can be selected and copied, to paste them exactly.
    /// </summary>
    internal sealed class ParameterStandardWindow : Window
    {
        private const double WINDOW_WIDTH = 1100;
        private const double WINDOW_HEIGHT = 720;
        private const double BODY_FONT_SIZE = 12;
        private const double HEADING_FONT_SIZE = 15;
        private const double TITLE_FONT_SIZE = 18;
        private const double SPACE_BETWEEN_SECTIONS = 20;
        private const double CELL_PADDING = 8;
        private static readonly Brush RULE_LINE_BRUSH = new SolidColorBrush(Color.FromRgb(0xD0, 0xD4, 0xD9));
        private static readonly Brush HEADER_ROW_BRUSH = new SolidColorBrush(Color.FromRgb(0xF0, 0xF2, 0xF4));

        public ParameterStandardWindow(string productName, StandardContent standard, IntPtr revitWindowHandle)
        {
            Title = productName + ": parameter standard";
            Width = WINDOW_WIDTH;
            Height = WINDOW_HEIGHT;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = BODY_FONT_SIZE;
            Background = Brushes.White;
            new WindowInteropHelper(this).Owner = revitWindowHandle;

            var page = new StackPanel { Margin = new Thickness(24) };
            page.Children.Add(new TextBlock { Text = "Parameter standard", FontSize = TITLE_FONT_SIZE, FontWeight = FontWeights.SemiBold });
            page.Children.Add(new TextBlock { Text = "Agreed " + standard.AgreedOn + ". Spell every value exactly as listed.", Margin = new Thickness(0, 4, 0, 0) });
            page.Children.Add(new TextBlock { Text = SourceSentence(standard), Margin = new Thickness(0, 2, 0, 0) });
            page.Children.Add(ParameterTable(standard));
            page.Children.Add(Heading("Parameter settings"));
            page.Children.Add(Paragraph(standard.ParameterSettings));
            page.Children.Add(Heading("Rules"));
            int ruleNumber = 1;
            foreach (string rule in standard.Rules)
            {
                page.Children.Add(Paragraph(ruleNumber + ". " + rule));
                ruleNumber++;
            }
            page.Children.Add(Heading("Examples"));
            page.Children.Add(ExampleTable(standard));

            var closeButton = new Button { Content = "Close", IsCancel = true, IsDefault = true, MinWidth = 88, Padding = new Thickness(12, 4, 12, 4), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 12, 24, 16) };
            closeButton.Click += (sender, arguments) => Close();

            var layout = new DockPanel();
            DockPanel.SetDock(closeButton, Dock.Bottom);
            layout.Children.Add(closeButton);
            layout.Children.Add(new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = layout;
        }

        /// <summary>Which copy of the standard this is, so the technician knows whether it is the website's latest.</summary>
        private static string SourceSentence(StandardContent standard)
        {
            return standard.IsFromWebsite
                ? "From the website (version " + standard.WebsiteVersion + ")."
                : "Built into this add-in: the website's copy has not reached this PC yet.";
        }

        private static Grid ParameterTable(StandardContent standard)
        {
            var table = new Grid { Margin = new Thickness(0, SPACE_BETWEEN_SECTIONS, 0, 0) };
            foreach (double columnWeight in new[] { 1.3, 1.1, 1.4, 2.2, 1.1 })
            {
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(columnWeight, GridUnitType.Star) });
            }
            AddRow(table, true, "Parameter (exact name)", "Allowed values (spell exactly)", "Revit categories (bind to)", "Fill it on", "If left blank, the website shows");
            foreach (StandardParameter parameter in standard.Parameters)
            {
                string allowedValues = string.Join(Environment.NewLine, parameter.AllowedValues);
                if (parameter.OtherValuesNote != null)
                {
                    allowedValues += Environment.NewLine + parameter.OtherValuesNote;
                }
                int rowIndex = AddRow(table, false,
                    parameter.Name + Environment.NewLine + parameter.Records,
                    allowedValues,
                    string.Join(", ", parameter.Categories),
                    parameter.FillOn,
                    parameter.IfBlank);
                ShowNameInBold(table, rowIndex, parameter);
            }
            return table;
        }

        private static Grid ExampleTable(StandardContent standard)
        {
            var table = new Grid();
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
            foreach (KeyValuePair<string, string> example in standard.Examples)
            {
                AddRow(table, false, example.Key, example.Value);
            }
            return table;
        }

        /// <summary>The first cell of a parameter's row: its name in bold, then what it records.</summary>
        private static void ShowNameInBold(Grid table, int rowIndex, StandardParameter parameter)
        {
            foreach (UIElement cell in table.Children)
            {
                if (Grid.GetRow(cell) == rowIndex && Grid.GetColumn(cell) == 0 && cell is Border border)
                {
                    var nameAndRecords = new StackPanel();
                    nameAndRecords.Children.Add(SelectableText(parameter.Name, true));
                    nameAndRecords.Children.Add(SelectableText(parameter.Records, false));
                    border.Child = nameAndRecords;
                }
            }
        }

        private static int AddRow(Grid table, bool isHeader, params string[] cellTexts)
        {
            int rowIndex = table.RowDefinitions.Count;
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int columnIndex = 0; columnIndex < cellTexts.Length; columnIndex++)
            {
                var cell = new Border
                {
                    BorderBrush = RULE_LINE_BRUSH,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Background = isHeader ? HEADER_ROW_BRUSH : Brushes.Transparent,
                    Padding = new Thickness(CELL_PADDING),
                    Child = SelectableText(cellTexts[columnIndex], isHeader),
                };
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, columnIndex);
                table.Children.Add(cell);
            }
            return rowIndex;
        }

        /// <summary>Text that looks like a label but can be selected and copied (a borderless read-only box).</summary>
        private static TextBox SelectableText(string text, bool isBold)
        {
            return new TextBox
            {
                Text = text,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(0),
                FontWeight = isBold ? FontWeights.SemiBold : FontWeights.Normal,
            };
        }

        private static TextBlock Heading(string text)
        {
            return new TextBlock { Text = text, FontSize = HEADING_FONT_SIZE, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, SPACE_BETWEEN_SECTIONS, 0, 6) };
        }

        private static TextBox Paragraph(string text)
        {
            TextBox paragraph = SelectableText(text, false);
            paragraph.Margin = new Thickness(0, 0, 0, 4);
            return paragraph;
        }
    }
}
