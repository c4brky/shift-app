// created by c4brky
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShiftMaker.Models;

namespace ShiftMaker.Services;

public class ImageExportService
{
    public string ExportScheduleImage(
        WeeklySchedule schedule,
        string targetFolder,
        ImageExportFormat format,
        AppThemeMode themeMode = AppThemeMode.Dark)
    {
        Directory.CreateDirectory(targetFolder);

        string extension = format == ImageExportFormat.Jpg ? ".jpg" : ".png";
        string filePath = Path.Combine(targetFolder, $"{schedule.FileBaseName}{extension}");

        var visual = BuildExportVisual(schedule, themeMode);

        const double width = 1420;
        visual.Width = width;
        visual.Measure(new Size(width, double.PositiveInfinity));
        double height = Math.Max(440, visual.DesiredSize.Height);
        visual.Arrange(new Rect(0, 0, width, height));
        visual.UpdateLayout();

        const double dpiScale = 2.0;
        int pixelWidth = (int)Math.Ceiling(width * dpiScale);
        int pixelHeight = (int)Math.Ceiling(height * dpiScale);

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * dpiScale, 96 * dpiScale, PixelFormats.Pbgra32);
        rtb.Render(visual);

        BitmapEncoder encoder = format == ImageExportFormat.Jpg
            ? new JpegBitmapEncoder { QualityLevel = 95 }
            : new PngBitmapEncoder();

        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);

        return filePath;
    }

    private static Border BuildExportVisual(WeeklySchedule schedule, AppThemeMode themeMode)
    {
        bool dark = true;
        var lang = schedule.Language;

        var bgBrush = new SolidColorBrush(Color.FromRgb(20, 21, 24));
        var cardBrush = new SolidColorBrush(Color.FromRgb(28, 30, 34));
        var borderBrush = new SolidColorBrush(Color.FromRgb(47, 51, 59));
        var headerCellBg = Color.FromRgb(36, 39, 44);
        var rowEvenBg = Color.FromRgb(28, 30, 34);
        var rowOddBg = Color.FromRgb(33, 36, 41);
        var cellLineColor = Color.FromRgb(47, 51, 59);

        var textDark = new SolidColorBrush(Color.FromRgb(241, 243, 245));
        var textMuted = new SolidColorBrush(Color.FromRgb(160, 168, 180));
        var accentBrush = new SolidColorBrush(Color.FromRgb(96, 165, 250));

        bool showFull = schedule.HasFullShifts || schedule.Rows.Any(r => r.FullCount > 0);

        var root = new Border
        {
            Background = bgBrush,
            Padding = new Thickness(28)
        };

        var card = new Border
        {
            Background = cardBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(24)
        };

        var stack = new StackPanel();

        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        string startDayName = WeekDateHelper.GetDayName(schedule.WeekStartDate, 0, lang);
        string endDayName = WeekDateHelper.GetDayName(schedule.WeekStartDate, 6, lang);
        string staffWord = LocalizationService.T(lang, "Personel", "Staff");

        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock
        {
            Text = schedule.WeekDisplayLabel,
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = textDark
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = $"{schedule.WeekStartDate:dd.MM.yyyy} {startDayName}  —  {schedule.WeekEndDate:dd.MM.yyyy} {endDayName}  •  {schedule.Rows.Count} {staffWord}",
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize = 13,
            Foreground = textMuted,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(titleStack, 0);
        headerGrid.Children.Add(titleStack);

        var legendPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        legendPanel.Children.Add(CreateLegendPill(LocalizationService.GetShiftLabel(ShiftType.Morning, lang, false), schedule.MorningTime.Formatted, ShiftType.Morning, schedule));
        legendPanel.Children.Add(CreateLegendPill(LocalizationService.GetShiftLabel(ShiftType.Mid, lang, false), schedule.MidTime.Formatted, ShiftType.Mid, schedule));
        legendPanel.Children.Add(CreateLegendPill(LocalizationService.GetShiftLabel(ShiftType.Evening, lang, false), schedule.EveningTime.Formatted, ShiftType.Evening, schedule));
        if (showFull)
            legendPanel.Children.Add(CreateLegendPill(LocalizationService.GetShiftLabel(ShiftType.Full, lang, true), schedule.FullTime.Formatted, ShiftType.Full, schedule));
        legendPanel.Children.Add(CreateLegendPill(LocalizationService.GetShiftLabel(ShiftType.Off, lang, false), LocalizationService.GetLeaveSubLabel(lang), ShiftType.Off, schedule));

        Grid.SetColumn(legendPanel, 1);
        headerGrid.Children.Add(legendPanel);

        stack.Children.Add(headerGrid);

        var tableBorder = new Border
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(168) });
        for (int d = 0; d < 7; d++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (schedule.ShowSummaryColumns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            if (showFull)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int r = 0; r < schedule.Rows.Count; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddCell(grid, 0, 0, LocalizationService.T(lang, "PERSONEL", "STAFF"), headerCellBg, cellLineColor, textDark, true, false);
        for (int d = 0; d < 7; d++)
        {
            string dayHeader = WeekDateHelper.GetDayHeaderLabel(schedule.WeekStartDate, d, lang);
            AddCell(grid, 0, d + 1, dayHeader, headerCellBg, cellLineColor, textDark, true, true);
        }
        if (schedule.ShowSummaryColumns)
        {
            var fullPreset = ShiftColorPalette.GetPreset(schedule.FullColorKey, ShiftType.Full);
            AddCell(grid, 0, 8, LocalizationService.T(lang, "Sabah + Ara\n(Erken)", "Morning + Mid\n(Early)"), headerCellBg, cellLineColor, accentBrush, true, true);
            AddCell(grid, 0, 9, LocalizationService.GetShiftLabel(ShiftType.Evening, lang, false), headerCellBg, cellLineColor, textDark, true, true);
            if (showFull)
                AddCell(grid, 0, 10, "Full", headerCellBg, cellLineColor, new SolidColorBrush(fullPreset.Fg), true, true);
        }

        for (int r = 0; r < schedule.Rows.Count; r++)
        {
            var row = schedule.Rows[r];
            var rowBg = r % 2 == 0 ? rowEvenBg : rowOddBg;

            AddCell(grid, r + 1, 0, row.PersonName, rowBg, cellLineColor, textDark, true, false);

            for (int d = 0; d < 7; d++)
            {
                var st = row.Days[d];
                var (label, sub, pillBg, pillBorder, pillFg) = GetShiftColors(st, schedule, dark);
                AddShiftBadgeCell(grid, r + 1, d + 1, label, schedule.ShowHoursInTable ? sub : null,
                    rowBg, cellLineColor, pillBg, pillBorder, pillFg);
            }

            if (schedule.ShowSummaryColumns)
            {
                var fullPreset = ShiftColorPalette.GetPreset(schedule.FullColorKey, ShiftType.Full);
                string mAbbr = lang == AppLanguage.En ? "M" : "S";
                string midAbbr = lang == AppLanguage.En ? "Mid" : "A";
                AddCell(grid, r + 1, 8, $"{row.EarlyTotal} ({row.MorningCount}{mAbbr}+{row.MidCount}{midAbbr})", rowBg, cellLineColor, accentBrush, true, true);
                AddCell(grid, r + 1, 9, $"{row.EveningCount}", rowBg, cellLineColor, textDark, false, true);
                if (showFull)
                    AddCell(grid, r + 1, 10, $"{row.FullCount}", rowBg, cellLineColor,
                        new SolidColorBrush(fullPreset.Fg), row.FullCount > 0, true);
            }
        }

        tableBorder.Child = grid;
        stack.Children.Add(tableBorder);

        string createdLabel = LocalizationService.T(lang, "Oluşturulma", "Created");
        var footer = new TextBlock
        {
            Text = $"{createdLabel}: {schedule.CreatedAt:dd.MM.yyyy HH:mm}  •  SHIFT MAKER",
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize = 11,
            Foreground = textMuted,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 2, 0)
        };
        stack.Children.Add(footer);

        card.Child = stack;
        root.Child = card;
        return root;
    }

    public static (string label, string sub, Color bg, Color border, Color fg) GetShiftColors(
        ShiftType st,
        WeeklySchedule? schedule,
        bool dark,
        CustomizationSettings? settings = null)
    {
        var lang = schedule?.Language ?? settings?.Language ?? AppLanguage.Tr;
        string morningSub = schedule?.MorningTime.Formatted ?? settings?.MorningTime.Formatted ?? "";
        string midSub = schedule?.MidTime.Formatted ?? settings?.MidTime.Formatted ?? "";
        string eveningSub = schedule?.EveningTime.Formatted ?? settings?.EveningTime.Formatted ?? "";
        string fullSub = schedule?.FullTime.Formatted ?? settings?.FullTime.Formatted ?? "";

        string colorKey = schedule != null
            ? ShiftColorPalette.GetKeyForShift(st, schedule)
            : ShiftColorPalette.GetKeyForShift(st, settings);

        var preset = ShiftColorPalette.GetPreset(colorKey, st);
        string label = LocalizationService.GetShiftLabel(st, lang, true);
        string sub = st switch
        {
            ShiftType.Morning => morningSub,
            ShiftType.Mid => midSub,
            ShiftType.Evening => eveningSub,
            ShiftType.Off => LocalizationService.GetLeaveSubLabel(lang),
            ShiftType.Full => fullSub,
            _ => ""
        };

        return (label, sub, preset.Bg, preset.Border, preset.Fg);
    }

    private static Border CreateLegendPill(string title, string sub, ShiftType st, WeeklySchedule schedule)
    {
        var (_, _, bg, border, fg) = GetShiftColors(st, schedule, true);
        var b = new Border
        {
            Background = new SolidColorBrush(bg),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(6, 0, 0, 0)
        };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(fg)
        });
        sp.Children.Add(new TextBlock
        {
            Text = sub,
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize = 10,
            Foreground = new SolidColorBrush(fg)
        });
        b.Child = sp;
        return b;
    }

    private static void AddCell(
        Grid grid, int row, int col, string text,
        Color bgColor, Color lineColor, Brush fgBrush, bool bold, bool centered)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(bgColor),
            BorderBrush = new SolidColorBrush(lineColor),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(10, 10, 10, 10)
        };

        var tb = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize = 12.5,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = fgBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left
        };

        border.Child = tb;
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        grid.Children.Add(border);
    }

    private static void AddShiftBadgeCell(
        Grid grid, int row, int col, string label, string? sub,
        Color rowBg, Color lineColor, Color pillBg, Color pillBorder, Color pillFg)
    {
        var cellBorder = new Border
        {
            Background = new SolidColorBrush(rowBg),
            BorderBrush = new SolidColorBrush(lineColor),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(6)
        };

        var pill = new Border
        {
            Background = new SolidColorBrush(pillBg),
            BorderBrush = new SolidColorBrush(pillBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 6, 6, 6),
            VerticalAlignment = VerticalAlignment.Center
        };

        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(pillFg),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        if (!string.IsNullOrEmpty(sub))
        {
            sp.Children.Add(new TextBlock
            {
                Text = sub,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                FontSize = 10,
                Foreground = new SolidColorBrush(pillFg),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                Opacity = 0.92
            });
        }

        pill.Child = sp;
        cellBorder.Child = pill;
        Grid.SetRow(cellBorder, row);
        Grid.SetColumn(cellBorder, col);
        grid.Children.Add(cellBorder);
    }
}
