// Hey! Sonunda beni bulabildin, seni tebrik ediyorum. <3
// made by c4brky
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using ShiftHazirlayici.Helpers;
using ShiftHazirlayici.Models;
using ShiftHazirlayici.Services;

namespace ShiftHazirlayici.Views;

public partial class CustomizeWindow : Window
{
    public CustomizationSettings WorkingSettings { get; private set; }
    private readonly IReadOnlyList<PersonConfig> _people;
    private bool _isInitializing = true;
    private bool _isGiftOpen = false;

    private class FullDayOption
    {
        public string? PersonId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public override string ToString() => DisplayName;
    }

    public CustomizeWindow(CustomizationSettings currentSettings, IReadOnlyList<PersonConfig> people)
    {
        Resources.Add("BoolToVis", new BooleanToVisibilityConverter());
        InitializeComponent();

        _people = people;
        WorkingSettings = currentSettings.Clone();
        WorkingSettings.ThemeMode = AppThemeMode.Dark;
        DataContext = WorkingSettings;

        SourceInitialized += (_, _) => WindowsThemeHelper.ApplyModernWindowChromeIfSupported(this, true);

        PopulateDropdowns();
        LoadValuesToDropdowns();
        BuildFullDaySelectors();
        _isInitializing = false;
    }

    private void PopulateDropdowns()
    {
        var hourItems = WeekDateHelper.ValidHours.Select(h => $"{h:00}").ToList();
        var minItems = WeekDateHelper.ValidMinutes.Select(m => $"{m:00}").ToList();

        foreach (var cmb in new[]
                 {
                     CmbSabahStartH, CmbSabahEndH, CmbAraStartH, CmbAraEndH,
                     CmbAksamStartH, CmbAksamEndH, CmbFullStartH, CmbFullEndH
                 })
            cmb.ItemsSource = hourItems;

        foreach (var cmb in new[]
                 {
                     CmbSabahStartM, CmbSabahEndM, CmbAraStartM, CmbAraEndM,
                     CmbAksamStartM, CmbAksamEndM, CmbFullStartM, CmbFullEndM
                 })
            cmb.ItemsSource = minItems;

        CmbDailySabah.ItemsSource = new[] { 1, 2, 3, 4 };
        CmbDailyAra.ItemsSource = new[] { 0, 1, 2, 3, 4 };
    }

    private void LoadValuesToDropdowns()
    {
        SetTimeCombo(WorkingSettings.SabahTime, CmbSabahStartH, CmbSabahStartM, CmbSabahEndH, CmbSabahEndM);
        SetTimeCombo(WorkingSettings.AraTime, CmbAraStartH, CmbAraStartM, CmbAraEndH, CmbAraEndM);
        SetTimeCombo(WorkingSettings.AksamTime, CmbAksamStartH, CmbAksamStartM, CmbAksamEndH, CmbAksamEndM);
        SetTimeCombo(WorkingSettings.FullTime, CmbFullStartH, CmbFullStartM, CmbFullEndH, CmbFullEndM);

        CmbRandomScope.SelectedIndex = (int)WorkingSettings.RandomLeaveScope;
        CmbDailySabah.SelectedItem = WorkingSettings.DailySabahCount;
        CmbDailyAra.SelectedItem = WorkingSettings.DailyAraCount;
        CmbExportFormat.SelectedIndex = (int)WorkingSettings.ExportFormat;
    }

    private void BuildFullDaySelectors()
    {
        FullDaysPanel.Children.Clear();
        WorkingSettings.FullPersonIdByDay = CustomizationSettings.NormalizeFullList(WorkingSettings.FullPersonIdByDay);

        var options = new List<FullDayOption>
        {
            new() { PersonId = null, DisplayName = "— Full Yok —" }
        };
        foreach (var p in _people)
        {
            string label = string.IsNullOrWhiteSpace(p.Name) ? "(İsimsiz Kişi)" : p.Name.Trim();
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
                Text = $"{PersonConfig.DayNamesTr[dayIndex]}:",
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
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
        ReadTimeCombo(WorkingSettings.SabahTime, CmbSabahStartH, CmbSabahStartM, CmbSabahEndH, CmbSabahEndM);
        ReadTimeCombo(WorkingSettings.AraTime, CmbAraStartH, CmbAraStartM, CmbAraEndH, CmbAraEndM);
        ReadTimeCombo(WorkingSettings.AksamTime, CmbAksamStartH, CmbAksamStartM, CmbAksamEndH, CmbAksamEndM);
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
        if (CmbDailySabah.SelectedItem is int s) WorkingSettings.DailySabahCount = s;
        if (CmbDailyAra.SelectedItem is int a) WorkingSettings.DailyAraCount = a;
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
        _isInitializing = true;
        WorkingSettings = new CustomizationSettings { ThemeMode = AppThemeMode.Dark };
        DataContext = WorkingSettings;
        LoadValuesToDropdowns();
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

    private void BtnCustomizeHeart_Click(object sender, RoutedEventArgs e)
    {
        _isGiftOpen = !_isGiftOpen;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        CustomizeGiftPill.BeginAnimation(WidthProperty, new DoubleAnimation
        {
            To = _isGiftOpen ? 165 : 0,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = ease
        });
        CustomizeGiftPill.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = _isGiftOpen ? 1.0 : 0.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = ease
        });
    }
}
