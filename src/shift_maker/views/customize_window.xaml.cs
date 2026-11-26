// made by c4brky
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShiftMaker.Helpers;
using ShiftMaker.Models;
using ShiftMaker.Services;

namespace ShiftMaker.Views;

public partial class CustomizeWindow : Window
{
    public CustomizationSettings WorkingSettings { get; private set; }
    private readonly IReadOnlyList<PersonConfig> _people;
    private readonly DateTime _weekStartDate;
    private bool _isInitializing = true;

    private AppLanguage Lang => WorkingSettings.Language;

    private class FullDayOption
    {
        public string? PersonId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public override string ToString() => DisplayName;
    }

    public CustomizeWindow(CustomizationSettings currentSettings, IReadOnlyList<PersonConfig> people, DateTime? weekStartDate = null)
    {
        Resources.Add("BoolToVis", new BooleanToVisibilityConverter());
        InitializeComponent();

        _people = people;
        _weekStartDate = (weekStartDate ?? DateTime.Today).Date;
        WorkingSettings = currentSettings.Clone();
        WorkingSettings.ThemeMode = AppThemeMode.Dark;
        DataContext = WorkingSettings;

        SourceInitialized += (_, _) => WindowsThemeHelper.ApplyModernWindowChromeIfSupported(this, true);

        PopulateDropdowns();
        LoadValuesToDropdowns();
        ApplyLanguage();
        BuildColorPickers();
        BuildFullDaySelectors();
        _isInitializing = false;
    }

    private void ApplyLanguage()
    {
        Title = "SHIFT MAKER — Customize";
        TxtHeaderTitle.Text = LocalizationService.T(Lang, "⚙ Özelleştirme", "⚙ Customize");
        BtnGitHubInfo.ToolTip = LocalizationService.T(Lang, "GitHub — Proje Sayfasını Aç", "GitHub — Open Project Page");

        TxtColorsHeader.Text = LocalizationService.T(Lang, "🎨 Shift Renkleri", "🎨 Shift Colors");
        TxtCustomHoursHeader.Text = LocalizationService.T(Lang, "Mesai Saatleri (07:00 – 22:00)", "Work Hours (07:00 – 22:00)");

        LblMorning.Text = LocalizationService.GetShiftLabel(ShiftType.Morning, Lang, false) + ":";
        LblMid.Text = LocalizationService.GetShiftLabel(ShiftType.Mid, Lang, false) + ":";
        LblEvening.Text = LocalizationService.GetShiftLabel(ShiftType.Evening, Lang, false) + ":";
        LblFull.Text = LocalizationService.T(Lang, "Full Kalma:", "Full Shift:");

        TxtFullHeader.Text = LocalizationService.T(Lang, "Full Kalma (İsteğe Bağlı)", "Full Shift (Optional)");
        BtnClearFull.Content = LocalizationService.T(Lang, "Tüm Full Seçimlerini Temizle", "Clear All Full Selections");

        TxtLeaveHeader.Text = LocalizationService.T(Lang, "İzin Kuralları & Toplu Seçim", "Leave Rules & Bulk Options");
        string lastDayName = WeekDateHelper.GetDayName(_weekStartDate, 6, Lang);
        ChkCustBulkLastDay.Content = LocalizationService.T(Lang, $"Toplu {lastDayName} İzni", $"Bulk {lastDayName} Leave");
        ChkCustNoLeave.Content = LocalizationService.T(Lang, "Bu hafta hiç izin verilmesin", "Disable leave for this week");
        ChkCustOverlap.Content = LocalizationService.T(Lang, "İzin günlerinin çakışmasına izin ver", "Allow leave day overlaps");
        LblRandomScope.Text = LocalizationService.T(Lang, "Rastgele İzin Kapsamı:", "Random Leave Scope:");

        CmbRandomScope.Items.Clear();
        string d0 = WeekDateHelper.GetDayName(_weekStartDate, 0, Lang);
        string d4 = WeekDateHelper.GetDayName(_weekStartDate, 4, Lang);
        string d5 = WeekDateHelper.GetDayName(_weekStartDate, 5, Lang);
        string d6 = WeekDateHelper.GetDayName(_weekStartDate, 6, Lang);
        CmbRandomScope.Items.Add(LocalizationService.T(Lang, $"İlk 5 Gün ({d0} - {d4})", $"First 5 Days ({d0} - {d4})"));
        CmbRandomScope.Items.Add(LocalizationService.T(Lang, $"İlk 6 Gün ({d0} - {d5})", $"First 6 Days ({d0} - {d5})"));
        CmbRandomScope.Items.Add(LocalizationService.T(Lang, $"Tüm Hafta ({d0} - {d6})", $"Full Week ({d0} - {d6})"));
        CmbRandomScope.SelectedIndex = Math.Clamp((int)WorkingSettings.RandomLeaveScope, 0, 2);

        TxtDistHeader.Text = LocalizationService.T(Lang, "Günlük Shift Sayıları", "Daily Shift Counts");
        LblDailyMorning.Text = LocalizationService.T(Lang, "Günlük Sabahçı:", "Daily Morning:");
        LblDailyMid.Text = LocalizationService.T(Lang, "Günlük Ara Shift:", "Daily Mid Shift:");
        ChkTwoWorkerPreferEvening.Content = LocalizationService.T(
            Lang,
            "2 kişi kaldığında: 1 Sabahçı + 1 Akşamcı",
            "With 2 workers: 1 Morning + 1 Evening");

        TxtRulesHeader.Text = LocalizationService.T(Lang, "Shift Kuralları", "Schedule Rules");
        ChkRuleBeforeAfter.Content = LocalizationService.T(
            Lang,
            "İzin öncesi Sabahçı, izin sonrası Akşamcı",
            "Morning before leave, Evening after leave");
        ChkRuleFair.Content = LocalizationService.T(
            Lang,
            "Sabahçı + Ara Shift eşit dağıtılsın",
            "Balance Morning + Mid shifts equally");
        ChkRuleHistory.Content = LocalizationService.T(
            Lang,
            "Önceki 2 haftayla farklılık sağla",
            "Avoid repeating last 2 weeks");
        ChkRuleHistoryRelax.Content = LocalizationService.T(
            Lang,
            "Gerektiğinde önceki hafta koşulunu esnet",
            "Relax history check when constraints are tight");

        TxtExportHeader.Text = LocalizationService.T(Lang, "Görsel Çıktı (PNG / JPG) & Tablo", "Image Export (PNG / JPG) & Table");
        LblExportFormat.Text = LocalizationService.T(Lang, "Görsel Formatı:", "Image Format:");
        ChkShowHours.Content = LocalizationService.T(Lang, "Hücre içinde mesai saatlerini göster", "Show shift hours inside cells");
        ChkShowSummary.Content = LocalizationService.T(Lang, "Sağda özet sütunlarını göster", "Show summary columns on the right");

        BtnResetDefaults.Content = LocalizationService.T(Lang, "Varsayılana Sıfırla", "Reset Defaults");
        BtnCancel.Content = LocalizationService.T(Lang, "İptal", "Cancel");
        BtnSave.Content = LocalizationService.T(Lang, "✓ Uygula", "✓ Apply");

        UpdateWorkHourLabelColors();
    }

    private void UpdateWorkHourLabelColors()
    {
        LblMorning.Foreground = new SolidColorBrush(ShiftColorPalette.GetPreset(WorkingSettings.MorningColorKey, ShiftType.Morning).Fg);
        LblMid.Foreground = new SolidColorBrush(ShiftColorPalette.GetPreset(WorkingSettings.MidColorKey, ShiftType.Mid).Fg);
        LblEvening.Foreground = new SolidColorBrush(ShiftColorPalette.GetPreset(WorkingSettings.EveningColorKey, ShiftType.Evening).Fg);
        LblFull.Foreground = new SolidColorBrush(ShiftColorPalette.GetPreset(WorkingSettings.FullColorKey, ShiftType.Full).Fg);
    }

    private void BuildColorPickers()
    {
        ColorPickersPanel.Children.Clear();

        var shiftTypes = new[]
        {
            ShiftType.Morning,
            ShiftType.Mid,
            ShiftType.Evening,
            ShiftType.Full,
            ShiftType.Off
        };

        foreach (var st in shiftTypes)
        {
            var currentKey = ShiftColorPalette.GetKeyForShift(st, WorkingSettings);
            var activePreset = ShiftColorPalette.GetPreset(currentKey, st);

            var rowBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 7)
            };
            rowBorder.SetResourceReference(Border.BackgroundProperty, "SubtleFillBrush");
            rowBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var previewPill = new Border
            {
                Background = new SolidColorBrush(activePreset.Bg),
                BorderBrush = new SolidColorBrush(activePreset.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 5, 10, 5),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var previewStack = new StackPanel();
            previewStack.Children.Add(new TextBlock
            {
                Text = LocalizationService.GetShiftLabel(st, Lang, false),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(activePreset.Fg)
            });
            previewStack.Children.Add(new TextBlock
            {
                Text = Lang == AppLanguage.En ? activePreset.NameEn : activePreset.NameTr,
                FontSize = 10,
                Foreground = new SolidColorBrush(activePreset.Fg),
                Opacity = 0.88
            });
            previewPill.Child = previewStack;
            Grid.SetColumn(previewPill, 0);
            grid.Children.Add(previewPill);

            var swatchesPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            foreach (var preset in ShiftColorPalette.Presets)
            {
                bool isSelected = string.Equals(preset.Key, activePreset.Key, StringComparison.OrdinalIgnoreCase);

                var swatchBtn = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(preset.Bg),
                    BorderBrush = isSelected
                        ? new SolidColorBrush(Colors.White)
                        : new SolidColorBrush(preset.Border),
                    BorderThickness = new Thickness(isSelected ? 2.2 : 1.2),
                    Margin = new Thickness(0, 2, 5, 2),
                    Cursor = Cursors.Hand,
                    ToolTip = Lang == AppLanguage.En ? preset.NameEn : preset.NameTr
                };

                var dot = new Border
                {
                    Width = 10,
                    Height = 10,
                    CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(preset.Fg),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                swatchBtn.Child = dot;

                var capturedType = st;
                var capturedKey = preset.Key;
                swatchBtn.MouseLeftButtonUp += (_, _) =>
                {
                    SetColorKey(capturedType, capturedKey);
                    BuildColorPickers();
                    UpdateWorkHourLabelColors();
                };

                swatchesPanel.Children.Add(swatchBtn);
            }

            Grid.SetColumn(swatchesPanel, 1);
            grid.Children.Add(swatchesPanel);

            rowBorder.Child = grid;
            ColorPickersPanel.Children.Add(rowBorder);
        }
    }

    private void SetColorKey(ShiftType st, string key)
    {
        switch (st)
        {
            case ShiftType.Morning: WorkingSettings.MorningColorKey = key; break;
            case ShiftType.Mid: WorkingSettings.MidColorKey = key; break;
            case ShiftType.Evening: WorkingSettings.EveningColorKey = key; break;
            case ShiftType.Full: WorkingSettings.FullColorKey = key; break;
            case ShiftType.Off: WorkingSettings.OffColorKey = key; break;
        }
    }

    private void PopulateDropdowns()
    {
        var hourItems = WeekDateHelper.ValidHours.Select(h => $"{h:00}").ToList();
        var minItems = WeekDateHelper.ValidMinutes.Select(m => $"{m:00}").ToList();

        foreach (var cmb in new[]
                 {
                     CmbMorningStartH, CmbMorningEndH, CmbMidStartH, CmbMidEndH,
                     CmbEveningStartH, CmbEveningEndH, CmbFullStartH, CmbFullEndH
                 })
            cmb.ItemsSource = hourItems;

        foreach (var cmb in new[]
                 {
                     CmbMorningStartM, CmbMorningEndM, CmbMidStartM, CmbMidEndM,
                     CmbEveningStartM, CmbEveningEndM, CmbFullStartM, CmbFullEndM
                 })
            cmb.ItemsSource = minItems;

        int teamCount = Math.Clamp(_people.Count, 3, 30);
        int maxMorning = Math.Max(1, teamCount - 1);
        int maxMid = Math.Max(1, teamCount - 1);

        CmbDailyMorning.ItemsSource = Enumerable.Range(1, maxMorning).ToList();
        CmbDailyMid.ItemsSource = Enumerable.Range(0, maxMid + 1).ToList();
    }

    private void LoadValuesToDropdowns()
    {
        SetTimeCombo(WorkingSettings.MorningTime, CmbMorningStartH, CmbMorningStartM, CmbMorningEndH, CmbMorningEndM);
        SetTimeCombo(WorkingSettings.MidTime, CmbMidStartH, CmbMidStartM, CmbMidEndH, CmbMidEndM);
        SetTimeCombo(WorkingSettings.EveningTime, CmbEveningStartH, CmbEveningStartM, CmbEveningEndH, CmbEveningEndM);
        SetTimeCombo(WorkingSettings.FullTime, CmbFullStartH, CmbFullStartM, CmbFullEndH, CmbFullEndM);

        int teamCount = Math.Clamp(_people.Count, 3, 30);
        WorkingSettings.DailyMorningCount = Math.Clamp(WorkingSettings.DailyMorningCount, 1, Math.Max(1, teamCount - 1));
        WorkingSettings.DailyMidCount = Math.Clamp(WorkingSettings.DailyMidCount, 0, Math.Max(1, teamCount - 1));

        CmbDailyMorning.SelectedItem = WorkingSettings.DailyMorningCount;
        CmbDailyMid.SelectedItem = WorkingSettings.DailyMidCount;
        CmbExportFormat.SelectedIndex = (int)WorkingSettings.ExportFormat;
    }

    private void BuildFullDaySelectors()
    {
        FullDaysPanel.Children.Clear();
        WorkingSettings.FullPersonIdByDay = CustomizationSettings.NormalizeFullList(WorkingSettings.FullPersonIdByDay);

        var options = new List<FullDayOption>
        {
            new() { PersonId = null, DisplayName = LocalizationService.T(Lang, "— Full Yok —", "— None —") }
        };
        foreach (var p in _people)
        {
            string label = string.IsNullOrWhiteSpace(p.Name)
                ? LocalizationService.T(Lang, "(İsimsiz Kişi)", "(Unnamed)")
                : p.Name.Trim();
            options.Add(new FullDayOption { PersonId = p.Id, DisplayName = label });
        }

        for (int d = 0; d < 7; d++)
        {
            int dayIndex = d;
            var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 7) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dayText = new TextBlock
            {
                Text = $"{WeekDateHelper.GetDayName(_weekStartDate, dayIndex, Lang)}:",
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            dayText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetColumn(dayText, 0);
            rowGrid.Children.Add(dayText);

            var cmb = new ComboBox
            {
                ItemsSource = options,
                DisplayMemberPath = nameof(FullDayOption.DisplayName)
            };

            string? selectedId = WorkingSettings.FullPersonIdByDay[dayIndex];
            var match = options.FirstOrDefault(o => string.Equals(o.PersonId, selectedId, StringComparison.Ordinal)) ?? options[0];
            cmb.SelectedItem = match;

            cmb.SelectionChanged += (_, _) =>
            {
                if (_isInitializing) return;
                if (cmb.SelectedItem is FullDayOption opt)
                    WorkingSettings.FullPersonIdByDay[dayIndex] = opt.PersonId;
            };

            Grid.SetColumn(cmb, 1);
            rowGrid.Children.Add(cmb);
            FullDaysPanel.Children.Add(rowGrid);
        }
    }

    private void SectionToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SectionsStack.InvalidateMeasure();
        MainScroll.InvalidateMeasure();
        UpdateLayout();
    }

    private static void SetTimeCombo(ShiftTimeRange tr, ComboBox sh, ComboBox sm, ComboBox eh, ComboBox em)
    {
        sh.SelectedItem = $"{Math.Clamp(tr.StartHour, 7, 22):00}";
        sm.SelectedItem = $"{NormalizeMinute(tr.StartMinute):00}";
        eh.SelectedItem = $"{Math.Clamp(tr.EndHour, 7, 22):00}";
        em.SelectedItem = $"{NormalizeMinute(tr.EndMinute):00}";
    }

    private static int NormalizeMinute(int m) =>
        WeekDateHelper.ValidMinutes.Contains(m) ? m : 0;

    private static void ReadTimeCombo(ShiftTimeRange tr, ComboBox sh, ComboBox sm, ComboBox eh, ComboBox em)
    {
        if (sh.SelectedItem is string s1 && int.TryParse(s1, out int v1)) tr.StartHour = v1;
        if (sm.SelectedItem is string s2 && int.TryParse(s2, out int v2)) tr.StartMinute = v2;
        if (eh.SelectedItem is string s3 && int.TryParse(s3, out int v3)) tr.EndHour = v3;
        if (em.SelectedItem is string s4 && int.TryParse(s4, out int v4)) tr.EndMinute = v4;
    }

    private void AnyTimeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        ReadTimeCombo(WorkingSettings.MorningTime, CmbMorningStartH, CmbMorningStartM, CmbMorningEndH, CmbMorningEndM);
        ReadTimeCombo(WorkingSettings.MidTime, CmbMidStartH, CmbMidStartM, CmbMidEndH, CmbMidEndM);
        ReadTimeCombo(WorkingSettings.EveningTime, CmbEveningStartH, CmbEveningStartM, CmbEveningEndH, CmbEveningEndM);
        ReadTimeCombo(WorkingSettings.FullTime, CmbFullStartH, CmbFullStartM, CmbFullEndH, CmbFullEndM);
    }

    private void CmbRandomScope_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (CmbRandomScope.SelectedIndex >= 0)
            WorkingSettings.RandomLeaveScope = (RandomLeaveScope)CmbRandomScope.SelectedIndex;
    }

    private void CmbDailyCountChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (CmbDailyMorning.SelectedItem is int m) WorkingSettings.DailyMorningCount = m;
        if (CmbDailyMid.SelectedItem is int mid) WorkingSettings.DailyMidCount = mid;
    }

    private void CmbExportFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (CmbExportFormat.SelectedIndex >= 0)
            WorkingSettings.ExportFormat = (ImageExportFormat)CmbExportFormat.SelectedIndex;
    }

    private void BtnClearFullSelections_Click(object sender, RoutedEventArgs e)
    {
        WorkingSettings.FullPersonIdByDay = CustomizationSettings.NormalizeFullList(null);
        _isInitializing = true;
        BuildFullDaySelectors();
        _isInitializing = false;
    }

    private void BtnResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var currentLang = WorkingSettings.Language;
        _isInitializing = true;
        WorkingSettings = new CustomizationSettings
        {
            ThemeMode = AppThemeMode.Dark,
            Language = currentLang
        };
        DataContext = WorkingSettings;
        LoadValuesToDropdowns();
        ApplyLanguage();
        BuildColorPickers();
        BuildFullDaySelectors();
        _isInitializing = false;
        SectionsStack.InvalidateMeasure();
        UpdateLayout();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void BtnGitHubInfo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/c4brky/shift-maker",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "SHIFT MAKER", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
