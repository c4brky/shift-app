// Hey! Sonunda beni bulabildin, seni tebrik ediyorum. <3
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
using ShiftHazirlayici.Helpers;
using ShiftHazirlayici.Models;
using ShiftHazirlayici.Services;
using ShiftHazirlayici.Views;

namespace ShiftHazirlayici;

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

        RefreshDynamicStatusAndRules();
        RenderShiftTable();
        RenderHistoryList();

        // İlk kez çalıştırıldığında kullanıcıdan kayıt konumunu göstermesini iste
        if (!_state.HasUserChosenSaveFolder)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(PromptFirstRunSaveFolder));
        }
    }

    private void PromptFirstRunSaveFolder()
    {
        MessageBox.Show(
            "Hoş geldiniz!\n\nOluşturulacak shift görsellerinin (PNG / JPG) kaydedileceği klasörü şimdi seçin.",
            "SHIFT HAZIRLAYICI — Kayıt Konumu",
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

    private void UpdateLegendPillColors()
    {
        ApplyLegendStyle(PillLegendSabah, LegendSabah, ShiftType.Sabah);
        ApplyLegendStyle(PillLegendAra, LegendAra, ShiftType.Ara);
        ApplyLegendStyle(PillLegendAksam, LegendAksam, ShiftType.Aksam);
        ApplyLegendStyle(PillLegendFull, LegendFull, ShiftType.Full);
        ApplyLegendStyle(PillLegendIzin, LegendIzin, ShiftType.Izin);
    }

    private static void ApplyLegendStyle(Border pill, TextBlock tb, ShiftType st)
    {
        var (_, _, bg, border, fg) = ImageExportService.GetShiftColors(st, null, true);
        pill.Background = new SolidColorBrush(bg);
        pill.BorderBrush = new SolidColorBrush(border);
        tb.Foreground = new SolidColorBrush(fg);
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
            TxtStatusAutoSave.Text = $"💾 Kaydedildi: {DateTime.Now:HH:mm:ss}";
        }
        catch
        {
            TxtStatusAutoSave.Text = "💾 Otomatik Kayıt: Beklemede";
        }
    }

    private void PopulateMainHourDropdowns()
    {
        var hours = WeekDateHelper.ValidHours.Select(h => $"{h:00}").ToList();
        var mins = WeekDateHelper.ValidMinutes.Select(m => $"{m:00}").ToList();

        foreach (var cmb in new[] { MainSabahSH, MainSabahEH, MainAraSH, MainAraEH, MainAksamSH, MainAksamEH, MainFullSH, MainFullEH })
            cmb.ItemsSource = hours;

        foreach (var cmb in new[] { MainSabahSM, MainSabahEM, MainAraSM, MainAraEM, MainAksamSM, MainAksamEM, MainFullSM, MainFullEM })
            cmb.ItemsSource = mins;
    }

    private void SyncUiFromState()
    {
        _isLoadingUi = true;

        TxtSaveFolder.Text = _state.SaveFolderPath;
        TxtSaveFolder.ToolTip = _state.SaveFolderPath;

        _state.TargetMondayDate = WeekDateHelper.GetMonday(_state.TargetMondayDate);
        DpTargetWeek.SelectedDate = _state.TargetMondayDate;
        TxtActiveWeekLabel.Text = WeekDateHelper.GetWeekDisplayLabel(_state.TargetMondayDate);

        ChkBulkSunday.IsChecked = _state.Settings.BulkSundayLeave;
        ChkNoLeaveAll.IsChecked = _state.Settings.NoLeaveForAll;
        CmbQuickLeaveScope.SelectedIndex = (int)_state.Settings.RandomLeaveScope;

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
        SetTimeCombo(_state.Settings.SabahTime, MainSabahSH, MainSabahSM, MainSabahEH, MainSabahEM);
        SetTimeCombo(_state.Settings.AraTime, MainAraSH, MainAraSM, MainAraEH, MainAraEM);
        SetTimeCombo(_state.Settings.AksamTime, MainAksamSH, MainAksamSM, MainAksamEH, MainAksamEM);
        SetTimeCombo(_state.Settings.FullTime, MainFullSH, MainFullSM, MainFullEH, MainFullEM);

        LegendSabah.Text = $"● Sabahçı: {_state.Settings.SabahTime.Formatted}";
        LegendAra.Text = $"● Ara Shift: {_state.Settings.AraTime.Formatted}";
        LegendAksam.Text = $"● Akşamcı: {_state.Settings.AksamTime.Formatted}";
        LegendFull.Text = $"● FULL: {_state.Settings.FullTime.Formatted}";
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
        bool disableIndividualLeaves = _state.Settings.BulkSundayLeave || _state.Settings.NoLeaveForAll;

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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 85 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
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
                ToolTip = "Personel adını girin"
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
                ToolTip = "İzin tercihi"
            };
            cmbMode.Items.Add("Rastgele");
            cmbMode.Items.Add("Belirli Gün");
            cmbMode.Items.Add("İzin Yok");
            cmbMode.SelectedIndex = (int)person.LeaveMode;

            var cmbDay = new ComboBox
            {
                Height = 29,
                IsEnabled = !disableIndividualLeaves && person.LeaveMode == LeaveMode.BelirliGun,
                ToolTip = "Sabit izin günü"
            };
            foreach (string dayName in PersonConfig.DayNamesTr)
                cmbDay.Items.Add(dayName);
            cmbDay.SelectedIndex = Math.Clamp(person.SelectedDayIndex, 0, 6);

            cmbMode.SelectionChanged += (_, _) =>
            {
                if (_isLoadingUi) return;
                person.LeaveMode = (LeaveMode)Math.Clamp(cmbMode.SelectedIndex, 0, 2);
                cmbDay.IsEnabled = !disableIndividualLeaves && person.LeaveMode == LeaveMode.BelirliGun;
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
                ToolTip = _state.People.Count > 3 ? "Kişiyi listeden çıkar" : "En az 3 kişi gereklidir"
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

        BtnAddPerson.IsEnabled = _state.People.Count < 10;
        CheckManualLeaveOverlapWarning();
    }

    private void RebuildMainFullDaySelectors()
    {
        MainFullDaysPanel.Children.Clear();
        _state.Settings.FullPersonIdByDay = CustomizationSettings.NormalizeFullList(_state.Settings.FullPersonIdByDay);

        var options = new List<FullDayOption>
        {
            new() { PersonId = null, DisplayName = "— Full Yok —" }
        };
        foreach (var p in _state.People)
        {
            string label = string.IsNullOrWhiteSpace(p.Name) ? "(İsimsiz)" : p.Name.Trim();
            options.Add(new FullDayOption { PersonId = p.Id, DisplayName = label });
        }

        bool prevLoading = _isLoadingUi;
        _isLoadingUi = true;

        for (int d = 0; d < 7; d++)
        {
            int dayIndex = d;
            var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var tb = new TextBlock
            {
                Text = PersonConfig.DayNamesTr[dayIndex],
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
        if (_state.Settings.BulkSundayLeave || _state.Settings.NoLeaveForAll || _state.Settings.AllowLeaveOverlap)
        {
            BannerOverlapWarning.Visibility = Visibility.Collapsed;
            return;
        }

        var conflicts = _state.People
            .Where(p => p.LeaveMode == LeaveMode.BelirliGun)
            .GroupBy(p => p.SelectedDayIndex)
            .Where(g => g.Count() > 1)
            .Select(g => PersonConfig.DayNamesTr[g.Key])
            .ToList();

        if (conflicts.Count > 0)
        {
            TxtOverlapWarning.Text = $"⚠ İzin çakışması: {string.Join(", ", conflicts)} günü birden fazla kişiye seçildi. Toplu günler dışında izinler çakışamaz.";
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
        TxtStatusPeople.Text = $"👥 {count} / 10 Kişi";

        if (_state.Settings.NoLeaveForAll)
            TxtStatusLeave.Text = "🚫 İzin: Kapalı";
        else if (_state.Settings.BulkSundayLeave)
            TxtStatusLeave.Text = "📅 İzin: Toplu Pazar";
        else
        {
            int manual = _state.People.Count(p => p.LeaveMode == LeaveMode.BelirliGun);
            int rnd = _state.People.Count(p => p.LeaveMode == LeaveMode.Rastgele);
            TxtStatusLeave.Text = $"🌴 İzin: {manual} Sabit, {rnd} Rastgele";
        }

        var priorCount = GetApplicablePriorWeeksCount(_state.TargetMondayDate);
        TxtStatusHistory.Text = _state.Settings.EnableHistoryComparison
            ? $"🕒 Son 2 Hafta: {priorCount} kayıt"
            : "🕒 Önceki Hafta: Kapalı";

        TxtStatusExport.Text = _state.Settings.ExportFormat == ImageExportFormat.Jpg
            ? "🖼 Görsel: JPG"
            : "🖼 Görsel: PNG";

        bool showFullLegend = _state.Settings.EnableFullShifts || (_state.CurrentSchedule?.HasFullShifts ?? false);
        PillLegendFull.Visibility = showFullLegend ? Visibility.Visible : Visibility.Collapsed;

        var rules = ShiftGeneratorService.BuildActiveRulesList(_state.People, _state.Settings, priorCount);
        RulesListControl.ItemsSource = rules;
    }

    private int GetApplicablePriorWeeksCount(DateTime targetMonday)
    {
        var prior = _history.Where(s => s.WeekStartMonday.Date < targetMonday.Date).Take(2).Count();
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
        DateTime monday = isGenerated ? schedule!.WeekStartMonday : _state.TargetMondayDate;
        string weekLabel = isGenerated ? schedule!.WeekDisplayLabel : WeekDateHelper.GetWeekDisplayLabel(monday);

        TxtTableTitle.Text = $"Haftalık Shift Programı • {weekLabel}";

        if (isGenerated)
        {
            TxtScheduleState.Text = "✓ Hazır & Kayıtlı";
            TxtScheduleState.SetResourceReference(TextBlock.ForegroundProperty, "SuccessTextBrush");
            BadgeScheduleState.SetResourceReference(Border.BackgroundProperty, "SuccessBgBrush");
            TxtTableSubtitle.Text = $"Oluşturulma: {schedule!.CreatedAt:dd.MM.yyyy HH:mm}  •  Dosya: {schedule.FileBaseName}";

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
            TxtScheduleState.Text = "Canlı Taslak";
            TxtScheduleState.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            BadgeScheduleState.SetResourceReference(Border.BackgroundProperty, "SubtleFillBrush");
            TxtTableSubtitle.Text = $"{_state.People.Count} kişi için dinamik önizleme — oluşturmak için OLUŞTUR'a tıklayın.";
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

        // Anthracite dark gray table colors
        Color headerBg = Color.FromRgb(36, 39, 44);
        Color rowEven = Color.FromRgb(28, 30, 34);
        Color rowOdd = Color.FromRgb(33, 36, 41);
        Color lineColor = Color.FromRgb(47, 51, 59);

        AddHeaderCell(0, "PERSONEL", false, headerBg, lineColor);
        for (int d = 0; d < 7; d++)
            AddHeaderCell(d + 1, WeekDateHelper.GetDayHeaderLabel(monday, d), true, headerBg, lineColor);

        if (showSummary)
        {
            AddHeaderCell(8, "Sabah+Ara", true, headerBg, lineColor);
            AddHeaderCell(9, "Akşam", true, headerBg, lineColor);
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
                    AddShiftPillCell(r + 1, d + 1, personRow.Days[d], schedule, rowBg, lineColor);

                if (showSummary)
                {
                    AddSummaryCell(r + 1, 8, $"{personRow.EarlyTotal} ({personRow.SabahCount}S+{personRow.AraCount}A)", rowBg, lineColor, true);
                    AddSummaryCell(r + 1, 9, $"{personRow.AksamCount}", rowBg, lineColor, false);
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
                string displayName = string.IsNullOrWhiteSpace(p.Name) ? $"Kişi {r + 1}" : p.Name.Trim();

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

    private void AddShiftPillCell(int row, int col, ShiftType st, WeeklySchedule schedule, Color rowBg, Color lineColor)
    {
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
            Padding = new Thickness(6, 6, 6, 6)
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
        cell.Child = pill;
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        ShiftTableGrid.Children.Add(cell);
    }

    private void AddDraftCell(int row, int col, PersonConfig person, int dayIdx, Color rowBg, Color lineColor)
    {
        bool isKnownLeave =
            (!_state.Settings.NoLeaveForAll && _state.Settings.BulkSundayLeave && dayIdx == 6) ||
            (!_state.Settings.NoLeaveForAll && !_state.Settings.BulkSundayLeave && person.LeaveMode == LeaveMode.BelirliGun && person.SelectedDayIndex == dayIdx);

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
            var (_, _, bg, bd, fg) = ImageExportService.GetShiftColors(ShiftType.Izin, null, true);
            pill = CreateDraftPill("İZİN", bg, bd, fg, true);
        }
        else if (isKnownFull)
        {
            var (_, _, bg, bd, fg) = ImageExportService.GetShiftColors(ShiftType.Full, null, true);
            pill = CreateDraftPill("FULL", bg, bd, fg, true);
        }
        else
        {
            Color bg = Color.FromRgb(36, 39, 44);
            Color bd = Color.FromRgb(54, 58, 66);
            Color fg = Color.FromRgb(110, 119, 133);
            pill = CreateDraftPill("Bekliyor", bg, bd, fg, false);
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
        TabHistory.Content = $"🗂 Geçmiş ({_history.Count})";
    }

    private void RenderHistoryList()
    {
        HistoryListPanel.Children.Clear();
        UpdateHistoryTabTitle();

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
                Text = "Henüz kaydedilmiş geçmiş shift bulunmuyor. Ana ekrandan 'OLUŞTUR' butonuna bastığınızda kayıtlar burada listelenir.",
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
                ToolTip = "Tek tıkla bu haftanın shift planını aç"
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
                Content = "Aç / Görüntüle",
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
                    Content = "🖼 Görsel",
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
                ToolTip = "Kaydı geçmişten sil"
            };
            btnDel.Click += (_, e) =>
            {
                e.Handled = true;
                var res = MessageBox.Show(
                    $"'{item.WeekDisplayLabel}' tarihli shift kaydı geçmişten silinsin mi?",
                    "Kaydı Sil",
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
                Text = $"📅 {item.WeekDisplayLabel}   ({item.WeekStartMonday:dd.MM.yyyy} - {item.WeekEndSunday:dd.MM.yyyy})",
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            titleTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            info.Children.Add(titleTb);

            string namesSummary = string.Join(", ", item.Rows.Select(r => r.PersonName));
            var subTb = new TextBlock
            {
                Text = $"👥 {item.Rows.Count} Kişi ({namesSummary})  •  🕒 {item.CreatedAt:dd.MM.yyyy HH:mm}",
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
        _state.TargetMondayDate = item.WeekStartMonday;

        _isLoadingUi = true;
        DpTargetWeek.SelectedDate = _state.TargetMondayDate;
        TxtActiveWeekLabel.Text = WeekDateHelper.GetWeekDisplayLabel(_state.TargetMondayDate);
        _isLoadingUi = false;

        TabPlanner.IsChecked = true;
        RefreshDynamicStatusAndRules();
        RenderShiftTable();
        ScheduleAutoSave();
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    private void BtnSecretHeart_Click(object sender, RoutedEventArgs e)
    {
        _isGiftOpen = !_isGiftOpen;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var widthAnim = new DoubleAnimation
        {
            To = _isGiftOpen ? 168 : 0,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = ease
        };
        var opacityAnim = new DoubleAnimation
        {
            To = _isGiftOpen ? 1.0 : 0.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = ease
        };

        GiftMessagePill.BeginAnimation(WidthProperty, widthAnim);
        GiftMessagePill.BeginAnimation(OpacityProperty, opacityAnim);
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
        if (_state.People.Count >= 10) return;
        _state.People.Add(new PersonConfig
        {
            Name = $"Personel {_state.People.Count + 1}",
            LeaveMode = LeaveMode.Rastgele
        });
        RebuildPersonRows();
        RebuildMainFullDaySelectors();
        OnInputsChanged(invalidateSchedule: true);
    }

    private void QuickLeaveOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingUi) return;

        _isLoadingUi = true;
        if (sender == ChkBulkSunday)
        {
            _state.Settings.BulkSundayLeave = ChkBulkSunday.IsChecked == true;
            ChkNoLeaveAll.IsChecked = _state.Settings.NoLeaveForAll;
        }
        else if (sender == ChkNoLeaveAll)
        {
            _state.Settings.NoLeaveForAll = ChkNoLeaveAll.IsChecked == true;
            ChkBulkSunday.IsChecked = _state.Settings.BulkSundayLeave;
        }
        _isLoadingUi = false;

        RebuildPersonRows();
        OnInputsChanged(invalidateSchedule: true);
    }

    private void BtnAllRandomLeave_Click(object sender, RoutedEventArgs e)
    {
        _isLoadingUi = true;
        _state.Settings.BulkSundayLeave = false;
        _state.Settings.NoLeaveForAll = false;
        ChkBulkSunday.IsChecked = false;
        ChkNoLeaveAll.IsChecked = false;
        foreach (var p in _state.People)
            p.LeaveMode = LeaveMode.Rastgele;
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

        ReadTimeCombo(_state.Settings.SabahTime, MainSabahSH, MainSabahSM, MainSabahEH, MainSabahEM);
        ReadTimeCombo(_state.Settings.AraTime, MainAraSH, MainAraSM, MainAraEH, MainAraEM);
        ReadTimeCombo(_state.Settings.AksamTime, MainAksamSH, MainAksamSM, MainAksamEH, MainAksamEM);
        ReadTimeCombo(_state.Settings.FullTime, MainFullSH, MainFullSM, MainFullEH, MainFullEM);

        LegendSabah.Text = $"● Sabahçı: {_state.Settings.SabahTime.Formatted}";
        LegendAra.Text = $"● Ara Shift: {_state.Settings.AraTime.Formatted}";
        LegendAksam.Text = $"● Akşamcı: {_state.Settings.AksamTime.Formatted}";
        LegendFull.Text = $"● FULL: {_state.Settings.FullTime.Formatted}";

        RefreshDynamicStatusAndRules();
        ScheduleAutoSave();
    }

    private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
    {
        PromptAndSelectSaveFolder();
    }

    private bool PromptAndSelectSaveFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Shift görsellerinin kaydedileceği klasörü seçin",
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
            MessageBox.Show($"Klasör açılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnPrevWeek_Click(object sender, RoutedEventArgs e) =>
        SetTargetWeek(_state.TargetMondayDate.AddDays(-7));

    private void BtnNextWeek_Click(object sender, RoutedEventArgs e) =>
        SetTargetWeek(_state.TargetMondayDate.AddDays(7));

    private void DpTargetWeek_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi || !DpTargetWeek.SelectedDate.HasValue) return;
        SetTargetWeek(DpTargetWeek.SelectedDate.Value);
    }

    private void SetTargetWeek(DateTime date)
    {
        _isLoadingUi = true;
        _state.TargetMondayDate = WeekDateHelper.GetMonday(date);
        DpTargetWeek.SelectedDate = _state.TargetMondayDate;
        TxtActiveWeekLabel.Text = WeekDateHelper.GetWeekDisplayLabel(_state.TargetMondayDate);
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

    private void BtnBackToPlanner_Click(object sender, RoutedEventArgs e)
    {
        TabPlanner.IsChecked = true;
    }

    private void BtnCustomize_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CustomizeWindow(_state.Settings, _state.People) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _state.Settings = dlg.WorkingSettings;
            _state.Settings.ThemeMode = AppThemeMode.Dark;
            SyncUiFromState();
            OnInputsChanged(invalidateSchedule: true);
        }
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (!_state.HasUserChosenSaveFolder)
        {
            var answer = MessageBox.Show(
                $"Henüz kayıt konumu seçmediniz.\n\nOluşturulacak shift görseli şu klasöre kaydedilsin mi?\n{_state.SaveFolderPath}\n\nFarklı klasör seçmek için 'Hayır'a tıklayın.",
                "Kayıt Konumu Seçimi",
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

        var result = _generator.Generate(_state.People, _state.TargetMondayDate, _state.Settings, _history);
        if (!result.Success || result.Schedule == null)
        {
            MessageBox.Show(result.ErrorMessage ?? "Shift oluşturulamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show($"Shift oluşturuldu ancak görsel kaydedilemedi:\n{ex.Message}", "Görsel Kayıt Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            string imgPath = _exporter.ExportScheduleImage(_state.CurrentSchedule, _state.SaveFolderPath, _state.Settings.ExportFormat, AppThemeMode.Dark);
            _state.CurrentSchedule.ImageFilePath = imgPath;
            RenderShiftTable();
            PerformAutoSave();
            MessageBox.Show($"Görsel kaydedildi:\n{imgPath}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Görsel aktarılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show($"Dosya açılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
