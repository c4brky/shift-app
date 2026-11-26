// made by c4brky
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using ShiftMaker.Helpers;
using ShiftMaker.Models;
using ShiftMaker.Services;
using ShiftMaker.Views;

namespace ShiftMaker;

public partial class MainWindow : Window
{
    private readonly PersistenceService _persistence = new();
    private readonly ShiftGeneratorService _generator = new();
    private readonly ImageExportService _exporter = new();
    private readonly DispatcherTimer _autoSaveTimer;

    private AppState _state = new();
    private List<WeeklySchedule> _history = new();
    private bool _isLoadingUi = true;
    private bool _isGiftOpen = false;

    private AppLanguage Lang => _state.Settings.Language;

    private class FullDayOption
    {
        public string? PersonId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public override string ToString() => DisplayName;
    }

    public MainWindow()
    {
        InitializeComponent();

        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _autoSaveTimer.Tick += (_, _) =>
        {
            _autoSaveTimer.Stop();
            PerformAutoSave();
        };

        SourceInitialized += (_, _) => WindowsThemeHelper.ApplyModernWindowChromeIfSupported(this, true);
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoadingUi = true;

        PopulateMainHourDropdowns();
        _state = _persistence.LoadOrCreateDefaultState();
        _state.Settings.ThemeMode = AppThemeMode.Dark;
        _history = _persistence.LoadHistory();

        UpdateLegendPillColors();
        SyncUiFromState();
        _isLoadingUi = false;

        ApplyLanguage();
        RefreshDynamicStatusAndRules();
        RenderShiftTable();
        RenderHistoryList();

        if (!_state.HasUserChosenSaveFolder)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(PromptFirstRunSaveFolder));
        }
    }

    private void PromptFirstRunSaveFolder()
    {
        MessageBox.Show(
            LocalizationService.T(Lang,
                "Hoş geldiniz!\n\nOluşturulacak shift görsellerinin (PNG / JPG) kaydedileceği klasörü şimdi seçin.",
                "Welcome!\n\nPlease choose the folder where exported shift images (PNG / JPG) will be saved."),
            "SHIFT MAKER",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        if (PromptAndSelectSaveFolder())
        {
            _state.HasUserChosenSaveFolder = true;
            PerformAutoSave();
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _autoSaveTimer.Stop();
        PerformAutoSave();
    }

    private void ApplyLanguage()
    {
        bool prevLoading = _isLoadingUi;
        _isLoadingUi = true;

        BtnLanguage.Content = Lang == AppLanguage.En ? "EN" : "TR";
        BtnLanguage.ToolTip = "Language: TR / EN";

        TxtSaveLabel.Text = LocalizationService.T(Lang, "📁 Kayıt:", "📁 Save:");
        BtnSelectFolder.Content = LocalizationService.T(Lang, "Seç", "Select");
        BtnOpenFolder.Content = LocalizationService.T(Lang, "Aç", "Open");
        BtnOpenFolder.ToolTip = LocalizationService.T(Lang, "Kayıt klasörünü aç", "Open save folder");

        TabPlanner.Content = LocalizationService.T(Lang, "📋 Planlayıcı", "📋 Planner");
        UpdateHistoryTabTitle();

        BtnPrevWeek.ToolTip = LocalizationService.T(Lang, "Önceki Hafta", "Previous Week");
        BtnNextWeek.ToolTip = LocalizationService.T(Lang, "Sonraki Hafta", "Next Week");
        DpStartDate.ToolTip = LocalizationService.T(Lang, "Başlangıç Tarihi", "Start Date");
        DpEndDate.ToolTip = LocalizationService.T(Lang, "Bitiş Tarihi", "End Date");

        BtnCustomize.Content = LocalizationService.T(Lang, "⚙ ÖZELLEŞTİR", "⚙ CUSTOMIZE");
        BtnGenerate.Content = LocalizationService.T(Lang, "✨ OLUŞTUR", "✨ GENERATE");

        SubTabTeam.Content = LocalizationService.T(Lang, "👥 Ekip & İzinler", "👥 Team & Leave");
        SubTabHoursFull.Content = LocalizationService.T(Lang, "⏰ Saatler & Full", "⏰ Hours & Full");

        TxtStaffListTitle.Text = LocalizationService.T(Lang, "Personel Listesi (3–30)", "Staff List (3–30)");
        BtnAddPerson.Content = LocalizationService.T(Lang, "+ Kişi Ekle", "+ Add Staff");

        TxtBulkLeaveHeader.Text = LocalizationService.T(Lang, "Toplu İzin & Kapsam", "Bulk Leave & Scope");
        string lastDayName = WeekDateHelper.GetDayName(_state.TargetStartDate, 6, Lang);
        ChkBulkLastDay.Content = LocalizationService.T(Lang, $"Toplu {lastDayName} İzni", $"Bulk {lastDayName} Leave");
        ChkNoLeaveAll.Content = LocalizationService.T(Lang, "İzinleri Kapat", "Disable All Leave");
        BtnAllRandomLeave.Content = LocalizationService.T(Lang, "Herkes Rastgele", "All Random");

        RefreshLeaveScopeComboItems();

        TxtWorkHoursHeader.Text = LocalizationService.T(Lang, "Mesai Saatleri (07–22)", "Work Hours (07–22)");
        LblMainMorning.Text = LocalizationService.GetShiftLabel(ShiftType.Morning, Lang, false);
        LblMainMid.Text = LocalizationService.GetShiftLabel(ShiftType.Mid, Lang, false);
        LblMainEvening.Text = LocalizationService.GetShiftLabel(ShiftType.Evening, Lang, false);
        LblMainFull.Text = LocalizationService.T(Lang, "Full Kalma", "Full Shift");

        TxtEnableFullMain.Text = LocalizationService.T(Lang, "Full Kalma Ekle", "Add Full Shift");
        BtnClearMainFull.Content = LocalizationService.T(Lang, "Full Seçimlerini Temizle", "Clear Full Selections");

        BtnOpenExportedImage.Content = LocalizationService.T(Lang, "🖼 Görseli Aç", "🖼 Open Image");
        BtnReExportImage.Content = LocalizationService.T(Lang, "💾 Yeniden Dışa Aktar", "💾 Re-Export");

        TxtHistoryTitle.Text = LocalizationService.T(Lang, "🗂 Geçmiş Shift Kayıtları", "🗂 Shift History");
        BtnClearAllHistory.Content = LocalizationService.T(Lang, "🗑 Tüm Geçmişi Temizle", "🗑 Clear All History");

        TxtRulesFooterTitle.Text = LocalizationService.T(Lang, "📌 Aktif Kurallar", "📌 Active Rules");
        TxtGiftMessage.Text = "c4brky <3";

        UpdateLegendPillColors();
        UpdateLegendTexts();
        RebuildPersonRows();
        RebuildMainFullDaySelectors();

        _isLoadingUi = prevLoading;
    }

    private void RefreshLeaveScopeComboItems()
    {
        int prevIdx = CmbQuickLeaveScope.SelectedIndex;
        CmbQuickLeaveScope.Items.Clear();

        DateTime start = _state.TargetStartDate.Date;
        string d0 = WeekDateHelper.GetDayName(start, 0, Lang);
        string d4 = WeekDateHelper.GetDayName(start, 4, Lang);
        string d5 = WeekDateHelper.GetDayName(start, 5, Lang);
        string d6 = WeekDateHelper.GetDayName(start, 6, Lang);

        CmbQuickLeaveScope.Items.Add(LocalizationService.T(Lang, $"İlk 5 Gün ({d0}-{d4})", $"First 5 Days ({d0}-{d4})"));
        CmbQuickLeaveScope.Items.Add(LocalizationService.T(Lang, $"İlk 6 Gün ({d0}-{d5})", $"First 6 Days ({d0}-{d5})"));
        CmbQuickLeaveScope.Items.Add(LocalizationService.T(Lang, $"Tüm Hafta ({d0}-{d6})", $"Full Week ({d0}-{d6})"));

        CmbQuickLeaveScope.SelectedIndex = Math.Clamp(prevIdx >= 0 ? prevIdx : (int)_state.Settings.RandomLeaveScope, 0, 2);
    }

    private void UpdateLegendPillColors()
    {
        ApplyLegendStyle(PillLegendMorning, LegendMorning, ShiftType.Morning);
        ApplyLegendStyle(PillLegendMid, LegendMid, ShiftType.Mid);
        ApplyLegendStyle(PillLegendEvening, LegendEvening, ShiftType.Evening);
        ApplyLegendStyle(PillLegendFull, LegendFull, ShiftType.Full);
        ApplyLegendStyle(PillLegendOff, LegendOff, ShiftType.Off);

        LblMainMorning.Foreground = new SolidColorBrush(ShiftColorPalette.GetPreset(_state.Settings.MorningColorKey, ShiftType.Morning).Fg);
        LblMainMid.Foreground = new SolidColorBrush(ShiftColorPalette.GetPreset(_state.Settings.MidColorKey, ShiftType.Mid).Fg);
        LblMainEvening.Foreground = new SolidColorBrush(ShiftColorPalette.GetPreset(_state.Settings.EveningColorKey, ShiftType.Evening).Fg);
        LblMainFull.Foreground = new SolidColorBrush(ShiftColorPalette.GetPreset(_state.Settings.FullColorKey, ShiftType.Full).Fg);
    }

    private void ApplyLegendStyle(Border pill, TextBlock tb, ShiftType st)
    {
        var (_, _, bg, border, fg) = ImageExportService.GetShiftColors(st, null, true, _state.Settings);
        pill.Background = new SolidColorBrush(bg);
        pill.BorderBrush = new SolidColorBrush(border);
        tb.Foreground = new SolidColorBrush(fg);
    }

    private void UpdateLegendTexts()
    {
        LegendMorning.Text = $"● {LocalizationService.GetShiftLabel(ShiftType.Morning, Lang, false)}: {_state.Settings.MorningTime.Formatted}";
        LegendMid.Text = $"● {LocalizationService.GetShiftLabel(ShiftType.Mid, Lang, false)}: {_state.Settings.MidTime.Formatted}";
        LegendEvening.Text = $"● {LocalizationService.GetShiftLabel(ShiftType.Evening, Lang, false)}: {_state.Settings.EveningTime.Formatted}";
        LegendFull.Text = $"● {LocalizationService.GetShiftLabel(ShiftType.Full, Lang, true)}: {_state.Settings.FullTime.Formatted}";
        LegendOff.Text = $"● {LocalizationService.GetShiftLabel(ShiftType.Off, Lang, true)}: {LocalizationService.GetLeaveSubLabel(Lang)}";
    }

    private void ScheduleAutoSave()
    {
        if (_isLoadingUi) return;
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    private void PerformAutoSave()
    {
        try
        {
            _persistence.SaveAutoState(_state);
            TxtStatusAutoSave.Text = $"💾 {LocalizationService.T(Lang, "Kaydedildi", "Saved")}: {DateTime.Now:HH:mm:ss}";
        }
        catch
        {
            TxtStatusAutoSave.Text = $"💾 {LocalizationService.T(Lang, "Otomatik Kayıt: Beklemede", "Auto-save: Pending")}";
        }
    }

    private void PopulateMainHourDropdowns()
    {
        var hours = WeekDateHelper.ValidHours.Select(h => $"{h:00}").ToList();
        var mins = WeekDateHelper.ValidMinutes.Select(m => $"{m:00}").ToList();

        foreach (var cmb in new[] { MainMorningSH, MainMorningEH, MainMidSH, MainMidEH, MainEveningSH, MainEveningEH, MainFullSH, MainFullEH })
            cmb.ItemsSource = hours;

        foreach (var cmb in new[] { MainMorningSM, MainMorningEM, MainMidSM, MainMidEM, MainEveningSM, MainEveningEM, MainFullSM, MainFullEM })
            cmb.ItemsSource = mins;
    }

    private void SyncUiFromState()
    {
        _isLoadingUi = true;

        TxtSaveFolder.Text = _state.SaveFolderPath;
        TxtSaveFolder.ToolTip = _state.SaveFolderPath;

        if (_state.TargetStartDate == default)
            _state.TargetStartDate = WeekDateHelper.GetMonday(DateTime.Today);
        else
            _state.TargetStartDate = _state.TargetStartDate.Date;

        DpStartDate.SelectedDate = _state.TargetStartDate;
        DpEndDate.SelectedDate = WeekDateHelper.GetWeekEnd(_state.TargetStartDate);

        ChkBulkLastDay.IsChecked = _state.Settings.BulkLastDayLeave;
        ChkNoLeaveAll.IsChecked = _state.Settings.NoLeaveForAll;
        RefreshLeaveScopeComboItems();

        ChkEnableFullMain.IsChecked = _state.Settings.EnableFullShifts;
        MainFullDaysContainer.Visibility = _state.Settings.EnableFullShifts ? Visibility.Visible : Visibility.Collapsed;

        SyncHourCombosFromSettings();
        RebuildPersonRows();
        RebuildMainFullDaySelectors();
        UpdateHistoryTabTitle();

        _isLoadingUi = false;
    }

    private void SyncHourCombosFromSettings()
    {
        SetTimeCombo(_state.Settings.MorningTime, MainMorningSH, MainMorningSM, MainMorningEH, MainMorningEM);
        SetTimeCombo(_state.Settings.MidTime, MainMidSH, MainMidSM, MainMidEH, MainMidEM);
        SetTimeCombo(_state.Settings.EveningTime, MainEveningSH, MainEveningSM, MainEveningEH, MainEveningEM);
        SetTimeCombo(_state.Settings.FullTime, MainFullSH, MainFullSM, MainFullEH, MainFullEM);
        UpdateLegendTexts();
    }

    private static void SetTimeCombo(ShiftTimeRange tr, ComboBox sh, ComboBox sm, ComboBox eh, ComboBox em)
    {
        sh.SelectedItem = $"{Math.Clamp(tr.StartHour, 7, 22):00}";
        sm.SelectedItem = $"{(WeekDateHelper.ValidMinutes.Contains(tr.StartMinute) ? tr.StartMinute : 0):00}";
        eh.SelectedItem = $"{Math.Clamp(tr.EndHour, 7, 22):00}";
        em.SelectedItem = $"{(WeekDateHelper.ValidMinutes.Contains(tr.EndMinute) ? tr.EndMinute : 0):00}";
    }

    private static void ReadTimeCombo(ShiftTimeRange tr, ComboBox sh, ComboBox sm, ComboBox eh, ComboBox em)
    {
        if (sh.SelectedItem is string s1 && int.TryParse(s1, out int v1)) tr.StartHour = v1;
        if (sm.SelectedItem is string s2 && int.TryParse(s2, out int v2)) tr.StartMinute = v2;
        if (eh.SelectedItem is string s3 && int.TryParse(s3, out int v3)) tr.EndHour = v3;
        if (em.SelectedItem is string s4 && int.TryParse(s4, out int v4)) tr.EndMinute = v4;
    }

    private void RebuildPersonRows()
    {
        PersonRowsPanel.Children.Clear();
        bool disableIndividualLeaves = _state.Settings.BulkLastDayLeave || _state.Settings.NoLeaveForAll;
        DateTime weekStart = _state.TargetStartDate.Date;

        for (int i = 0; i < _state.People.Count; i++)
        {
            int idx = i;
            var person = _state.People[idx];

            var rowBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(7, 6, 7, 6),
                Margin = new Thickness(0, 0, 0, 6)
            };
            rowBorder.SetResourceReference(Border.BackgroundProperty, "SubtleFillBrush");
            rowBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            var numBadge = new Border
            {
                CornerRadius = new CornerRadius(6),
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center
            };
            numBadge.SetResourceReference(Border.BackgroundProperty, "AccentLightBrush");
            var numText = new TextBlock
            {
                Text = $"{idx + 1}",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            numText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            numBadge.Child = numText;
            Grid.SetColumn(numBadge, 0);
            grid.Children.Add(numBadge);

            var txtName = new TextBox
            {
                Text = person.Name,
                Height = 29,
                ToolTip = LocalizationService.T(Lang, "Personel adını girin", "Enter staff name")
            };
            txtName.TextChanged += (_, _) =>
            {
                if (_isLoadingUi) return;
                person.Name = txtName.Text;
                RebuildMainFullDaySelectors();
                OnInputsChanged(invalidateSchedule: true);
            };
            Grid.SetColumn(txtName, 1);
            grid.Children.Add(txtName);

            var cmbMode = new ComboBox
            {
                Height = 29,
                IsEnabled = !disableIndividualLeaves,
                ToolTip = LocalizationService.T(Lang, "İzin tercihi", "Leave mode")
            };
            cmbMode.Items.Add(LocalizationService.T(Lang, "Rastgele", "Random"));
            cmbMode.Items.Add(LocalizationService.T(Lang, "Belirli Gün", "Fixed Day"));
            cmbMode.Items.Add(LocalizationService.T(Lang, "İzin Yok", "No Leave"));
            cmbMode.SelectedIndex = (int)person.LeaveMode;

            var cmbDay = new ComboBox
            {
                Height = 29,
                IsEnabled = !disableIndividualLeaves && person.LeaveMode == LeaveMode.FixedDay,
                ToolTip = LocalizationService.T(Lang, "Sabit izin günü", "Fixed leave day")
            };
            for (int d = 0; d < 7; d++)
                cmbDay.Items.Add(WeekDateHelper.GetDayName(weekStart, d, Lang));
            cmbDay.SelectedIndex = Math.Clamp(person.SelectedDayIndex, 0, 6);

            cmbMode.SelectionChanged += (_, _) =>
            {
                if (_isLoadingUi) return;
                person.LeaveMode = (LeaveMode)Math.Clamp(cmbMode.SelectedIndex, 0, 2);
                cmbDay.IsEnabled = !disableIndividualLeaves && person.LeaveMode == LeaveMode.FixedDay;
                OnInputsChanged(invalidateSchedule: true);
            };

            cmbDay.SelectionChanged += (_, _) =>
            {
                if (_isLoadingUi) return;
                if (cmbDay.SelectedIndex >= 0)
                {
                    person.SelectedDayIndex = cmbDay.SelectedIndex;
                    OnInputsChanged(invalidateSchedule: true);
                }
            };

            Grid.SetColumn(cmbMode, 3);
            grid.Children.Add(cmbMode);

            Grid.SetColumn(cmbDay, 5);
            grid.Children.Add(cmbDay);

            var btnRemove = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(4, 0, 0, 0),
                Style = (Style)FindResource("SecondaryButton"),
                IsEnabled = _state.People.Count > 3,
                ToolTip = _state.People.Count > 3
                    ? LocalizationService.T(Lang, "Kişiyi listeden çıkar", "Remove staff")
                    : LocalizationService.T(Lang, "En az 3 kişi gereklidir", "Minimum 3 staff required")
            };
            btnRemove.Click += (_, _) =>
            {
                if (_state.People.Count <= 3) return;
                string removedId = _state.People[idx].Id;
                _state.People.RemoveAt(idx);
                for (int d = 0; d < _state.Settings.FullPersonIdByDay.Count; d++)
                {
                    if (_state.Settings.FullPersonIdByDay[d] == removedId)
                        _state.Settings.FullPersonIdByDay[d] = null;
                }
                RebuildPersonRows();
                RebuildMainFullDaySelectors();
                OnInputsChanged(invalidateSchedule: true);
            };
            Grid.SetColumn(btnRemove, 6);
            grid.Children.Add(btnRemove);

            rowBorder.Child = grid;
            PersonRowsPanel.Children.Add(rowBorder);
        }

        BtnAddPerson.IsEnabled = _state.People.Count < 30;
        CheckManualLeaveOverlapWarning();
    }

    private void RebuildMainFullDaySelectors()
    {
        MainFullDaysPanel.Children.Clear();
        _state.Settings.FullPersonIdByDay = CustomizationSettings.NormalizeFullList(_state.Settings.FullPersonIdByDay);
        DateTime weekStart = _state.TargetStartDate.Date;

        var options = new List<FullDayOption>
        {
            new() { PersonId = null, DisplayName = LocalizationService.T(Lang, "— Full Yok —", "— None —") }
        };
        foreach (var p in _state.People)
        {
            string label = string.IsNullOrWhiteSpace(p.Name)
                ? LocalizationService.T(Lang, "(İsimsiz)", "(Unnamed)")
                : p.Name.Trim();
            options.Add(new FullDayOption { PersonId = p.Id, DisplayName = label });
        }

        bool prevLoading = _isLoadingUi;
        _isLoadingUi = true;

        for (int d = 0; d < 7; d++)
        {
            int dayIndex = d;
            var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var tb = new TextBlock
            {
                Text = WeekDateHelper.GetDayName(weekStart, dayIndex, Lang),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetColumn(tb, 0);
            rowGrid.Children.Add(tb);

            var cmb = new ComboBox
            {
                Height = 28,
                ItemsSource = options,
                DisplayMemberPath = nameof(FullDayOption.DisplayName)
            };

            string? selectedId = _state.Settings.FullPersonIdByDay[dayIndex];
            cmb.SelectedItem = options.FirstOrDefault(o => string.Equals(o.PersonId, selectedId, StringComparison.Ordinal)) ?? options[0];

            cmb.SelectionChanged += (_, _) =>
            {
                if (_isLoadingUi) return;
                if (cmb.SelectedItem is FullDayOption opt)
                {
                    _state.Settings.FullPersonIdByDay[dayIndex] = opt.PersonId;
                    OnInputsChanged(invalidateSchedule: true);
                }
            };

            Grid.SetColumn(cmb, 1);
            rowGrid.Children.Add(cmb);
            MainFullDaysPanel.Children.Add(rowGrid);
        }

        _isLoadingUi = prevLoading;
    }

    private void CheckManualLeaveOverlapWarning()
    {
        if (_state.Settings.BulkLastDayLeave || _state.Settings.NoLeaveForAll || _state.Settings.AllowLeaveOverlap)
        {
            BannerOverlapWarning.Visibility = Visibility.Collapsed;
            return;
        }

        DateTime weekStart = _state.TargetStartDate.Date;
        var conflicts = _state.People
            .Where(p => p.LeaveMode == LeaveMode.FixedDay)
            .GroupBy(p => p.SelectedDayIndex)
            .Where(g => g.Count() > 1)
            .Select(g => WeekDateHelper.GetDayName(weekStart, g.Key, Lang))
            .ToList();

        if (conflicts.Count > 0)
        {
            TxtOverlapWarning.Text = LocalizationService.T(
                Lang,
                $"⚠ İzin çakışması: {string.Join(", ", conflicts)} günü birden fazla kişiye seçildi.",
                $"⚠ Leave overlap: {string.Join(", ", conflicts)} selected for multiple staff.");
            BannerOverlapWarning.Visibility = Visibility.Visible;
        }
        else
        {
            BannerOverlapWarning.Visibility = Visibility.Collapsed;
        }
    }

    private void OnInputsChanged(bool invalidateSchedule)
    {
        if (invalidateSchedule && _state.CurrentSchedule != null)
            _state.CurrentSchedule = null;

        CheckManualLeaveOverlapWarning();
        RefreshDynamicStatusAndRules();
        RenderShiftTable();
        ScheduleAutoSave();
    }

    private void RefreshDynamicStatusAndRules()
    {
        int count = _state.People.Count;
        TxtStatusPeople.Text = LocalizationService.T(Lang, $"👥 {count} / 30 Kişi", $"👥 {count} / 30 Staff");

        if (_state.Settings.NoLeaveForAll)
            TxtStatusLeave.Text = LocalizationService.T(Lang, "🚫 İzin: Kapalı", "🚫 Leave: Off");
        else if (_state.Settings.BulkLastDayLeave)
        {
            string d6 = WeekDateHelper.GetDayName(_state.TargetStartDate, 6, Lang);
            TxtStatusLeave.Text = LocalizationService.T(Lang, $"📅 İzin: Toplu {d6}", $"📅 Leave: Bulk {d6}");
        }
        else
        {
            int manual = _state.People.Count(p => p.LeaveMode == LeaveMode.FixedDay);
            int rnd = _state.People.Count(p => p.LeaveMode == LeaveMode.Random);
            TxtStatusLeave.Text = LocalizationService.T(Lang, $"🌴 İzin: {manual} Sabit, {rnd} Rastgele", $"🌴 Leave: {manual} Fixed, {rnd} Random");
        }

        var priorCount = GetApplicablePriorWeeksCount(_state.TargetStartDate);
        TxtStatusHistory.Text = _state.Settings.EnableHistoryComparison
            ? LocalizationService.T(Lang, $"🕒 Son 2 Hafta: {priorCount} kayıt", $"🕒 Last 2 Weeks: {priorCount}")
            : LocalizationService.T(Lang, "🕒 Önceki Hafta: Kapalı", "🕒 History Check: Off");

        TxtStatusExport.Text = _state.Settings.ExportFormat == ImageExportFormat.Jpg ? "🖼 JPG" : "🖼 PNG";

        bool showFullLegend = _state.Settings.EnableFullShifts || (_state.CurrentSchedule?.HasFullShifts ?? false);
        PillLegendFull.Visibility = showFullLegend ? Visibility.Visible : Visibility.Collapsed;

        var rules = ShiftGeneratorService.BuildActiveRulesList(_state.People, _state.Settings, priorCount, _state.TargetStartDate);
        RulesListControl.ItemsSource = rules;
    }

    private int GetApplicablePriorWeeksCount(DateTime targetStartDate)
    {
        var prior = _history.Where(s => s.WeekStartDate.Date < targetStartDate.Date).Take(2).Count();
        if (prior == 0 && _history.Count > 0)
            prior = Math.Min(2, _history.Count);
        return prior;
    }

    private void RenderShiftTable()
    {
        ShiftTableGrid.Children.Clear();
        ShiftTableGrid.ColumnDefinitions.Clear();
        ShiftTableGrid.RowDefinitions.Clear();

        var schedule = _state.CurrentSchedule;
        bool isGenerated = schedule != null;
        DateTime weekStart = isGenerated ? schedule!.WeekStartDate : _state.TargetStartDate;
        string weekLabel = isGenerated ? schedule!.WeekDisplayLabel : WeekDateHelper.GetWeekDisplayLabel(weekStart);

        TxtTableTitle.Text = weekLabel;

        if (isGenerated)
        {
            TxtScheduleState.Text = LocalizationService.T(Lang, "✓ Hazır", "✓ Ready");
            TxtScheduleState.SetResourceReference(TextBlock.ForegroundProperty, "SuccessTextBrush");
            BadgeScheduleState.SetResourceReference(Border.BackgroundProperty, "SuccessBgBrush");
            TxtTableSubtitle.Text = $"{schedule!.CreatedAt:dd.MM.yyyy HH:mm}  •  {schedule.FileBaseName}";
            TxtTableSubtitle.Visibility = Visibility.Visible;

            BtnOpenExportedImage.Visibility = !string.IsNullOrEmpty(schedule.ImageFilePath) && File.Exists(schedule.ImageFilePath)
                ? Visibility.Visible
                : Visibility.Collapsed;
            BtnReExportImage.Visibility = Visibility.Visible;

            if (schedule.Warnings.Count > 0)
            {
                TxtScheduleNotes.Text = string.Join("\n", schedule.Warnings.Select(w => "• " + w));
                BannerScheduleNotes.Visibility = Visibility.Visible;
            }
            else
            {
                BannerScheduleNotes.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            TxtScheduleState.Text = LocalizationService.T(Lang, "Taslak", "Draft");
            TxtScheduleState.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            BadgeScheduleState.SetResourceReference(Border.BackgroundProperty, "SubtleFillBrush");
            TxtTableSubtitle.Visibility = Visibility.Collapsed;
            BtnOpenExportedImage.Visibility = Visibility.Collapsed;
            BtnReExportImage.Visibility = Visibility.Collapsed;
            BannerScheduleNotes.Visibility = Visibility.Collapsed;
        }

        bool showSummary = isGenerated ? schedule!.ShowSummaryColumns : _state.Settings.ShowSummaryColumns;
        bool showFullCol = isGenerated
            ? (schedule!.HasFullShifts || schedule.Rows.Any(r => r.FullCount > 0))
            : (_state.Settings.EnableFullShifts && _state.Settings.FullPersonIdByDay.Any(id => !string.IsNullOrWhiteSpace(id)));

        ShiftTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(142) });
        for (int d = 0; d < 7; d++)
            ShiftTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 94 });

        if (showSummary)
        {
            ShiftTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            ShiftTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            if (showFullCol)
                ShiftTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
        }

        int rowCount = isGenerated ? schedule!.Rows.Count : _state.People.Count;
        ShiftTableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int r = 0; r < rowCount; r++)
            ShiftTableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Color headerBg = Color.FromRgb(36, 39, 44);
        Color rowEven = Color.FromRgb(28, 30, 34);
        Color rowOdd = Color.FromRgb(33, 36, 41);
        Color lineColor = Color.FromRgb(47, 51, 59);

        AddHeaderCell(0, LocalizationService.T(Lang, "PERSONEL", "STAFF"), false, headerBg, lineColor);
        for (int d = 0; d < 7; d++)
            AddHeaderCell(d + 1, WeekDateHelper.GetDayHeaderLabel(weekStart, d, Lang), true, headerBg, lineColor);

        if (showSummary)
        {
            AddHeaderCell(8, LocalizationService.T(Lang, "Sabah+Ara", "Morning+Mid"), true, headerBg, lineColor);
            AddHeaderCell(9, LocalizationService.GetShiftLabel(ShiftType.Evening, Lang, false), true, headerBg, lineColor);
            if (showFullCol)
                AddHeaderCell(10, "Full", true, headerBg, lineColor);
        }

        if (isGenerated)
        {
            for (int r = 0; r < schedule!.Rows.Count; r++)
            {
                var personRow = schedule.Rows[r];
                var rowBg = r % 2 == 0 ? rowEven : rowOdd;

                AddPersonNameCell(r + 1, personRow.PersonName, rowBg, lineColor);

                for (int d = 0; d < 7; d++)
                    AddShiftPillCell(r, d, personRow.Days[d], schedule, rowBg, lineColor);

                if (showSummary)
                {
                    string mAbbr = Lang == AppLanguage.En ? "M" : "S";
                    string midAbbr = Lang == AppLanguage.En ? "Mid" : "A";
                    AddSummaryCell(r + 1, 8, $"{personRow.EarlyTotal} ({personRow.MorningCount}{mAbbr}+{personRow.MidCount}{midAbbr})", rowBg, lineColor, true);
                    AddSummaryCell(r + 1, 9, $"{personRow.EveningCount}", rowBg, lineColor, false);
                    if (showFullCol)
                        AddSummaryCell(r + 1, 10, $"{personRow.FullCount}", rowBg, lineColor, personRow.FullCount > 0);
                }
            }
        }
        else
        {
            for (int r = 0; r < _state.People.Count; r++)
            {
                var p = _state.People[r];
                var rowBg = r % 2 == 0 ? rowEven : rowOdd;
                string displayName = string.IsNullOrWhiteSpace(p.Name)
                    ? LocalizationService.T(Lang, $"Kişi {r + 1}", $"Staff {r + 1}")
                    : p.Name.Trim();

                AddPersonNameCell(r + 1, displayName, rowBg, lineColor);

                for (int d = 0; d < 7; d++)
                    AddDraftCell(r + 1, d + 1, p, d, rowBg, lineColor);

                if (showSummary)
                {
                    AddSummaryCell(r + 1, 8, "—", rowBg, lineColor, false);
                    AddSummaryCell(r + 1, 9, "—", rowBg, lineColor, false);
                    if (showFullCol)
                        AddSummaryCell(r + 1, 10, "—", rowBg, lineColor, false);
                }
            }
        }
    }

    private void AddHeaderCell(int col, string text, bool centered, Color bg, Color lineColor)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(bg),
            BorderBrush = new SolidColorBrush(lineColor),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(8, 10, 8, 10)
        };
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left,
            HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        border.Child = tb;
        Grid.SetRow(border, 0);
        Grid.SetColumn(border, col);
        ShiftTableGrid.Children.Add(border);
    }

    private void AddPersonNameCell(int row, string name, Color bg, Color lineColor)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(bg),
            BorderBrush = new SolidColorBrush(lineColor),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(10, 10, 10, 10)
        };
        var tb = new TextBlock
        {
            Text = name,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        border.Child = tb;
        Grid.SetRow(border, row);
        Grid.SetColumn(border, 0);
        ShiftTableGrid.Children.Add(border);
    }

    private void AddShiftPillCell(int rowIdx, int dayIdx, ShiftType st, WeeklySchedule schedule, Color rowBg, Color lineColor)
    {
        schedule.Language = Lang;
        schedule.MorningColorKey = _state.Settings.MorningColorKey;
        schedule.MidColorKey = _state.Settings.MidColorKey;
        schedule.EveningColorKey = _state.Settings.EveningColorKey;
        schedule.FullColorKey = _state.Settings.FullColorKey;
        schedule.OffColorKey = _state.Settings.OffColorKey;

        var (label, timeText, pillBg, pillBorder, pillFg) = ImageExportService.GetShiftColors(st, schedule, true);

        var cell = new Border
        {
            Background = new SolidColorBrush(rowBg),
            BorderBrush = new SolidColorBrush(lineColor),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(5)
        };

        var pill = new Border
        {
            Background = new SolidColorBrush(pillBg),
            BorderBrush = new SolidColorBrush(pillBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 6, 6, 6),
            Cursor = Cursors.Hand,
            ToolTip = LocalizationService.T(Lang, "Değiştirmek için tıklayın", "Click to edit shift")
        };

        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(pillFg),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        if (schedule.ShowHoursInTable && !string.IsNullOrEmpty(timeText))
        {
            sp.Children.Add(new TextBlock
            {
                Text = timeText,
                FontSize = 10,
                Foreground = new SolidColorBrush(pillFg),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                Opacity = 0.92
            });
        }

        pill.Child = sp;

        pill.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            OpenShiftEditMenu(pill, schedule, rowIdx, dayIdx);
        };

        cell.Child = pill;
        Grid.SetRow(cell, rowIdx + 1);
        Grid.SetColumn(cell, dayIdx + 1);
        ShiftTableGrid.Children.Add(cell);
    }

    private void OpenShiftEditMenu(FrameworkElement target, WeeklySchedule schedule, int rowIdx, int dayIdx)
    {
        var menu = new ContextMenu();
        var shiftTypes = new[] { ShiftType.Morning, ShiftType.Mid, ShiftType.Evening, ShiftType.Full, ShiftType.Off };

        foreach (var candidate in shiftTypes)
        {
            var (_, _, _, _, fg) = ImageExportService.GetShiftColors(candidate, schedule, true);
            string shiftName = LocalizationService.GetShiftLabel(candidate, Lang, false);

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock
            {
                Text = "●",
                Foreground = new SolidColorBrush(fg),
                FontSize = 12,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = shiftName,
                VerticalAlignment = VerticalAlignment.Center
            });

            var item = new MenuItem { Header = headerPanel };
            var chosenType = candidate;
            item.Click += (_, _) =>
            {
                UpdateScheduleCell(schedule, rowIdx, dayIdx, chosenType);
            };
            menu.Items.Add(item);
        }

        menu.PlacementTarget = target;
        menu.IsOpen = true;
    }

    private void UpdateScheduleCell(WeeklySchedule schedule, int rowIdx, int dayIdx, ShiftType newType)
    {
        if (rowIdx < 0 || rowIdx >= schedule.Rows.Count || dayIdx < 0 || dayIdx >= 7) return;

        var row = schedule.Rows[rowIdx];
        row.Days[dayIdx] = newType;

        row.MorningCount = row.Days.Count(d => d == ShiftType.Morning);
        row.MidCount = row.Days.Count(d => d == ShiftType.Mid);
        row.EveningCount = row.Days.Count(d => d == ShiftType.Evening);
        row.OffCount = row.Days.Count(d => d == ShiftType.Off);
        row.FullCount = row.Days.Count(d => d == ShiftType.Full);

        schedule.HasFullShifts = schedule.Rows.Any(r => r.FullCount > 0);

        _persistence.SaveToHistory(schedule);
        _history = _persistence.LoadHistory();

        RefreshDynamicStatusAndRules();
        RenderShiftTable();
        RenderHistoryList();
        PerformAutoSave();
    }

    private void AddDraftCell(int row, int col, PersonConfig person, int dayIdx, Color rowBg, Color lineColor)
    {
        bool isKnownLeave =
            (!_state.Settings.NoLeaveForAll && _state.Settings.BulkLastDayLeave && dayIdx == 6) ||
            (!_state.Settings.NoLeaveForAll && !_state.Settings.BulkLastDayLeave && person.LeaveMode == LeaveMode.FixedDay && person.SelectedDayIndex == dayIdx);

        bool isKnownFull = _state.Settings.EnableFullShifts &&
                           dayIdx < _state.Settings.FullPersonIdByDay.Count &&
                           string.Equals(_state.Settings.FullPersonIdByDay[dayIdx], person.Id, StringComparison.Ordinal);

        var cell = new Border
        {
            Background = new SolidColorBrush(rowBg),
            BorderBrush = new SolidColorBrush(lineColor),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(5)
        };

        Border pill;
        if (isKnownLeave)
        {
            var (lbl, _, bg, bd, fg) = ImageExportService.GetShiftColors(ShiftType.Off, null, true, _state.Settings);
            pill = CreateDraftPill(lbl, bg, bd, fg, true);
        }
        else if (isKnownFull)
        {
            var (lbl, _, bg, bd, fg) = ImageExportService.GetShiftColors(ShiftType.Full, null, true, _state.Settings);
            pill = CreateDraftPill(lbl, bg, bd, fg, true);
        }
        else
        {
            Color bg = Color.FromRgb(36, 39, 44);
            Color bd = Color.FromRgb(54, 58, 66);
            Color fg = Color.FromRgb(110, 119, 133);
            pill = CreateDraftPill(LocalizationService.T(Lang, "Bekliyor", "Pending"), bg, bd, fg, false);
        }

        cell.Child = pill;
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        ShiftTableGrid.Children.Add(cell);
    }

    private static Border CreateDraftPill(string text, Color bg, Color border, Color fg, bool bold)
    {
        return new Border
        {
            Background = new SolidColorBrush(bg),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 8, 6, 8),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(fg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private void AddSummaryCell(int row, int col, string text, Color rowBg, Color lineColor, bool accent)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(rowBg),
            BorderBrush = new SolidColorBrush(lineColor),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(6, 8, 6, 8)
        };
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 11.5,
            FontWeight = accent ? FontWeights.Bold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, accent ? "AccentBrush" : "TextSecondaryBrush");
        border.Child = tb;
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        ShiftTableGrid.Children.Add(border);
    }

    private void UpdateHistoryTabTitle()
    {
        TabHistory.Content = LocalizationService.T(Lang, $"🗂 Geçmiş ({_history.Count})", $"🗂 History ({_history.Count})");
    }

    private void RenderHistoryList()
    {
        HistoryListPanel.Children.Clear();
        UpdateHistoryTabTitle();
        BtnClearAllHistory.IsEnabled = _history.Count > 0;

        if (_history.Count == 0)
        {
            var emptyCard = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(24)
            };
            emptyCard.SetResourceReference(Border.BackgroundProperty, "SubtleFillBrush");
            var tb = new TextBlock
            {
                Text = LocalizationService.T(
                    Lang,
                    "Henüz kaydedilmiş geçmiş shift bulunmuyor.",
                    "No saved shift history yet."),
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            emptyCard.Child = tb;
            HistoryListPanel.Children.Add(emptyCard);
            return;
        }

        foreach (var item in _history)
        {
            var card = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand,
                ToolTip = LocalizationService.T(Lang, "Açmak için tıklayın", "Click to open")
            };
            card.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            card.MouseEnter += (_, _) => card.SetResourceReference(Border.BackgroundProperty, "SurfaceHoverBrush");
            card.MouseLeave += (_, _) => card.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
            card.MouseLeftButtonUp += (_, _) => LoadScheduleFromHistory(item);

            var dock = new DockPanel();

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(actions, Dock.Right);

            var btnView = new Button
            {
                Content = LocalizationService.T(Lang, "Aç", "Open"),
                Style = (Style)FindResource("PrimaryButton"),
                Padding = new Thickness(12, 6, 12, 6),
                FontSize = 12,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnView.Click += (_, e) =>
            {
                e.Handled = true;
                LoadScheduleFromHistory(item);
            };
            actions.Children.Add(btnView);

            if (!string.IsNullOrEmpty(item.ImageFilePath) && File.Exists(item.ImageFilePath))
            {
                var btnImg = new Button
                {
                    Content = LocalizationService.T(Lang, "🖼 Görsel", "🖼 Image"),
                    Style = (Style)FindResource("SecondaryButton"),
                    Padding = new Thickness(10, 6, 10, 6),
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                btnImg.Click += (_, e) =>
                {
                    e.Handled = true;
                    OpenFileWithShell(item.ImageFilePath!);
                };
                actions.Children.Add(btnImg);
            }

            var btnDel = new Button
            {
                Content = "🗑",
                Style = (Style)FindResource("SecondaryButton"),
                Padding = new Thickness(9, 6, 9, 6),
                FontSize = 12,
                ToolTip = LocalizationService.T(Lang, "Kaydı sil", "Delete entry")
            };
            btnDel.Click += (_, e) =>
            {
                e.Handled = true;
                var res = MessageBox.Show(
                    LocalizationService.T(Lang,
                        $"'{item.WeekDisplayLabel}' tarihli shift kaydı geçmişten silinsin mi?",
                        $"Delete shift record '{item.WeekDisplayLabel}' from history?"),
                    LocalizationService.T(Lang, "Kaydı Sil", "Delete Record"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _persistence.DeleteFromHistory(item.Id);
                    _history = _persistence.LoadHistory();
                    RenderHistoryList();
                    RefreshDynamicStatusAndRules();
                }
            };
            actions.Children.Add(btnDel);

            dock.Children.Add(actions);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleTb = new TextBlock
            {
                Text = $"📅 {item.WeekDisplayLabel}   ({item.WeekStartDate:dd.MM.yyyy} - {item.WeekEndDate:dd.MM.yyyy})",
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            titleTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            info.Children.Add(titleTb);

            string namesSummary = string.Join(", ", item.Rows.Select(r => r.PersonName));
            string staffWord = LocalizationService.T(Lang, "Kişi", "Staff");
            var subTb = new TextBlock
            {
                Text = $"👥 {item.Rows.Count} {staffWord} ({namesSummary})  •  🕒 {item.CreatedAt:dd.MM.yyyy HH:mm}",
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 3, 12, 0)
            };
            subTb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            info.Children.Add(subTb);

            dock.Children.Add(info);
            card.Child = dock;
            HistoryListPanel.Children.Add(card);
        }
    }

    private void LoadScheduleFromHistory(WeeklySchedule item)
    {
        _state.CurrentSchedule = item;
        _state.TargetStartDate = item.WeekStartDate.Date;

        _isLoadingUi = true;
        DpStartDate.SelectedDate = _state.TargetStartDate;
        DpEndDate.SelectedDate = WeekDateHelper.GetWeekEnd(_state.TargetStartDate);
        _isLoadingUi = false;

        TabPlanner.IsChecked = true;
        ApplyLanguage();
        RefreshDynamicStatusAndRules();
        RenderShiftTable();
        ScheduleAutoSave();
    }

    private void BtnLanguage_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var trItem = new MenuItem { Header = "TR" };
        trItem.Click += (_, _) => SetLanguage(AppLanguage.Tr);

        var enItem = new MenuItem { Header = "EN" };
        enItem.Click += (_, _) => SetLanguage(AppLanguage.En);

        menu.Items.Add(trItem);
        menu.Items.Add(enItem);

        menu.PlacementTarget = BtnLanguage;
        menu.IsOpen = true;
    }

    private void SetLanguage(AppLanguage lang)
    {
        if (_state.Settings.Language == lang) return;
        _state.Settings.Language = lang;
        ApplyLanguage();
        RefreshDynamicStatusAndRules();
        RenderShiftTable();
        RenderHistoryList();
        ScheduleAutoSave();
    }

    private void BtnSecretHeart_Click(object sender, RoutedEventArgs e)
    {
        _isGiftOpen = !_isGiftOpen;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        GiftMessagePill.BeginAnimation(WidthProperty, new DoubleAnimation
        {
            To = _isGiftOpen ? 104 : 0,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = ease
        });
        GiftMessagePill.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = _isGiftOpen ? 1.0 : 0.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = ease
        });
    }

    private void ExpanderLayout_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingUi) return;
        LeftTeamStack?.InvalidateMeasure();
        LeftHoursFullStack?.InvalidateMeasure();
        UpdateLayout();
    }

    private void LeftSubTab_Changed(object sender, RoutedEventArgs e)
    {
        if (PanelLeftTeam == null || PanelLeftHoursFull == null) return;
        if (SubTabHoursFull.IsChecked == true)
        {
            PanelLeftTeam.Visibility = Visibility.Collapsed;
            PanelLeftHoursFull.Visibility = Visibility.Visible;
        }
        else
        {
            PanelLeftHoursFull.Visibility = Visibility.Collapsed;
            PanelLeftTeam.Visibility = Visibility.Visible;
        }
        UpdateLayout();
    }

    private void BtnAddPerson_Click(object sender, RoutedEventArgs e)
    {
        if (_state.People.Count >= 30) return;
        string prefix = LocalizationService.T(Lang, "Personel", "Staff");
        _state.People.Add(new PersonConfig
        {
            Name = $"{prefix} {_state.People.Count + 1}",
            LeaveMode = LeaveMode.Random
        });
        RebuildPersonRows();
        RebuildMainFullDaySelectors();
        OnInputsChanged(invalidateSchedule: true);
    }

    private void QuickLeaveOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingUi) return;

        _isLoadingUi = true;
        if (sender == ChkBulkLastDay)
        {
            _state.Settings.BulkLastDayLeave = ChkBulkLastDay.IsChecked == true;
            ChkNoLeaveAll.IsChecked = _state.Settings.NoLeaveForAll;
        }
        else if (sender == ChkNoLeaveAll)
        {
            _state.Settings.NoLeaveForAll = ChkNoLeaveAll.IsChecked == true;
            ChkBulkLastDay.IsChecked = _state.Settings.BulkLastDayLeave;
        }
        _isLoadingUi = false;

        RebuildPersonRows();
        OnInputsChanged(invalidateSchedule: true);
    }

    private void BtnAllRandomLeave_Click(object sender, RoutedEventArgs e)
    {
        _isLoadingUi = true;
        _state.Settings.BulkLastDayLeave = false;
        _state.Settings.NoLeaveForAll = false;
        ChkBulkLastDay.IsChecked = false;
        ChkNoLeaveAll.IsChecked = false;
        foreach (var p in _state.People)
            p.LeaveMode = LeaveMode.Random;
        _isLoadingUi = false;

        RebuildPersonRows();
        OnInputsChanged(invalidateSchedule: true);
    }

    private void CmbQuickLeaveScope_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi || CmbQuickLeaveScope.SelectedIndex < 0) return;
        _state.Settings.RandomLeaveScope = (RandomLeaveScope)CmbQuickLeaveScope.SelectedIndex;
        OnInputsChanged(invalidateSchedule: true);
    }

    private void ChkEnableFullMain_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingUi) return;
        bool enabled = ChkEnableFullMain.IsChecked == true;
        _state.Settings.EnableFullShifts = enabled;
        MainFullDaysContainer.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        LeftHoursFullStack.InvalidateMeasure();
        UpdateLayout();
        OnInputsChanged(invalidateSchedule: true);
    }

    private void BtnClearMainFull_Click(object sender, RoutedEventArgs e)
    {
        _state.Settings.FullPersonIdByDay = CustomizationSettings.NormalizeFullList(null);
        RebuildMainFullDaySelectors();
        OnInputsChanged(invalidateSchedule: true);
    }

    private void MainHourCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi) return;

        ReadTimeCombo(_state.Settings.MorningTime, MainMorningSH, MainMorningSM, MainMorningEH, MainMorningEM);
        ReadTimeCombo(_state.Settings.MidTime, MainMidSH, MainMidSM, MainMidEH, MainMidEM);
        ReadTimeCombo(_state.Settings.EveningTime, MainEveningSH, MainEveningSM, MainEveningEH, MainEveningEM);
        ReadTimeCombo(_state.Settings.FullTime, MainFullSH, MainFullSM, MainFullEH, MainFullEM);

        UpdateLegendTexts();
        RefreshDynamicStatusAndRules();
        ScheduleAutoSave();
    }

    private void BtnSelectFolder_Click(object sender, RoutedEventArgs e) => PromptAndSelectSaveFolder();

    private bool PromptAndSelectSaveFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = LocalizationService.T(Lang, "Shift görsellerinin kaydedileceği klasörü seçin", "Select folder to save shift images"),
            InitialDirectory = Directory.Exists(_state.SaveFolderPath)
                ? _state.SaveFolderPath
                : PersistenceService.GetDefaultSaveFolder()
        };

        if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            _state.SaveFolderPath = dialog.FolderName;
            _state.HasUserChosenSaveFolder = true;
            TxtSaveFolder.Text = _state.SaveFolderPath;
            TxtSaveFolder.ToolTip = _state.SaveFolderPath;
            ScheduleAutoSave();
            return true;
        }
        return false;
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_state.SaveFolderPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = _state.SaveFolderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                LocalizationService.T(Lang, $"Klasör açılamadı: {ex.Message}", $"Could not open folder: {ex.Message}"),
                LocalizationService.T(Lang, "Hata", "Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void BtnPrevWeek_Click(object sender, RoutedEventArgs e) =>
        SetTargetWeekStart(_state.TargetStartDate.AddDays(-7));

    private void BtnNextWeek_Click(object sender, RoutedEventArgs e) =>
        SetTargetWeekStart(_state.TargetStartDate.AddDays(7));

    private void DpStartDate_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi || !DpStartDate.SelectedDate.HasValue) return;
        SetTargetWeekStart(DpStartDate.SelectedDate.Value);
    }

    private void DpEndDate_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi || !DpEndDate.SelectedDate.HasValue) return;
        SetTargetWeekStart(DpEndDate.SelectedDate.Value.AddDays(-6));
    }

    private void SetTargetWeekStart(DateTime startDate)
    {
        _isLoadingUi = true;
        _state.TargetStartDate = startDate.Date;
        DpStartDate.SelectedDate = _state.TargetStartDate;
        DpEndDate.SelectedDate = WeekDateHelper.GetWeekEnd(_state.TargetStartDate);
        ApplyLanguage();
        _isLoadingUi = false;

        OnInputsChanged(invalidateSchedule: true);
    }

    private void NavTab_Changed(object sender, RoutedEventArgs e)
    {
        if (PanelPlanner == null || PanelHistory == null) return;
        if (TabHistory.IsChecked == true)
        {
            PanelPlanner.Visibility = Visibility.Collapsed;
            PanelHistory.Visibility = Visibility.Visible;
            RenderHistoryList();
        }
        else
        {
            PanelHistory.Visibility = Visibility.Collapsed;
            PanelPlanner.Visibility = Visibility.Visible;
        }
    }

    private void BtnClearAllHistory_Click(object sender, RoutedEventArgs e)
    {
        _persistence.ClearAllHistory();
        _history = _persistence.LoadHistory();
        RenderHistoryList();
        RefreshDynamicStatusAndRules();
    }

    private void BtnCustomize_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CustomizeWindow(_state.Settings, _state.People, _state.TargetStartDate) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _state.Settings = dlg.WorkingSettings;
            _state.Settings.ThemeMode = AppThemeMode.Dark;
            SyncUiFromState();
            ApplyLanguage();
            OnInputsChanged(invalidateSchedule: true);
        }
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (!_state.HasUserChosenSaveFolder)
        {
            var answer = MessageBox.Show(
                LocalizationService.T(Lang,
                    $"Henüz kayıt konumu seçmediniz.\n\nOluşturulacak shift görseli şu klasöre kaydedilsin mi?\n{_state.SaveFolderPath}\n\nFarklı klasör seçmek için 'Hayır'a tıklayın.",
                    $"You haven't selected a save folder yet.\n\nSave exported shift image to this folder?\n{_state.SaveFolderPath}\n\nClick 'No' to choose another folder."),
                LocalizationService.T(Lang, "Kayıt Konumu Seçimi", "Save Folder Selection"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel) return;
            if (answer == MessageBoxResult.No)
            {
                if (!PromptAndSelectSaveFolder()) return;
            }
            else
            {
                _state.HasUserChosenSaveFolder = true;
            }
        }

        var result = _generator.Generate(_state.People, _state.TargetStartDate, _state.Settings, _history);
        if (!result.Success || result.Schedule == null)
        {
            MessageBox.Show(
                result.ErrorMessage ?? LocalizationService.T(Lang, "Shift oluşturulamadı.", "Could not generate shift."),
                LocalizationService.T(Lang, "Uyarı", "Warning"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var schedule = result.Schedule;

        try
        {
            string imgPath = _exporter.ExportScheduleImage(schedule, _state.SaveFolderPath, _state.Settings.ExportFormat, AppThemeMode.Dark);
            schedule.ImageFilePath = imgPath;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                LocalizationService.T(Lang, $"Shift oluşturuldu ancak görsel kaydedilemedi:\n{ex.Message}", $"Schedule created but image could not be saved:\n{ex.Message}"),
                LocalizationService.T(Lang, "Görsel Kayıt Hatası", "Image Export Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        _state.CurrentSchedule = schedule;
        _persistence.SaveToHistory(schedule);
        _history = _persistence.LoadHistory();

        TabPlanner.IsChecked = true;
        RefreshDynamicStatusAndRules();
        RenderShiftTable();
        RenderHistoryList();
        PerformAutoSave();
    }

    private void BtnOpenExportedImage_Click(object sender, RoutedEventArgs e)
    {
        string? path = _state.CurrentSchedule?.ImageFilePath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            OpenFileWithShell(path);
    }

    private void BtnReExportImage_Click(object sender, RoutedEventArgs e)
    {
        if (_state.CurrentSchedule == null) return;
        try
        {
            _state.CurrentSchedule.ShowHoursInTable = _state.Settings.ShowHoursInTable;
            _state.CurrentSchedule.ShowSummaryColumns = _state.Settings.ShowSummaryColumns;
            _state.CurrentSchedule.Language = Lang;
            _state.CurrentSchedule.MorningColorKey = _state.Settings.MorningColorKey;
            _state.CurrentSchedule.MidColorKey = _state.Settings.MidColorKey;
            _state.CurrentSchedule.EveningColorKey = _state.Settings.EveningColorKey;
            _state.CurrentSchedule.FullColorKey = _state.Settings.FullColorKey;
            _state.CurrentSchedule.OffColorKey = _state.Settings.OffColorKey;

            string imgPath = _exporter.ExportScheduleImage(_state.CurrentSchedule, _state.SaveFolderPath, _state.Settings.ExportFormat, AppThemeMode.Dark);
            _state.CurrentSchedule.ImageFilePath = imgPath;
            RenderShiftTable();
            PerformAutoSave();
            MessageBox.Show(
                LocalizationService.T(Lang, $"Görsel kaydedildi:\n{imgPath}", $"Image saved:\n{imgPath}"),
                LocalizationService.T(Lang, "Başarılı", "Success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                LocalizationService.T(Lang, $"Görsel aktarılamadı: {ex.Message}", $"Could not export image: {ex.Message}"),
                LocalizationService.T(Lang, "Hata", "Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void OpenFileWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
