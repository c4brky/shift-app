// Hey! Sonunda beni bulabildin, seni tebrik ediyorum. <3
// created by c4brky
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ShiftHazirlayici.Models;

public enum ShiftType
{
    Sabah = 0,
    Ara = 1,
    Aksam = 2,
    Izin = 3,
    Full = 4
}

public enum LeaveMode
{
    Rastgele = 0,
    BelirliGun = 1,
    IzinYok = 2
}

public enum RandomLeaveScope
{
    HaftaIciPztCum = 0,
    PztCumartesi = 1,
    TumHafta = 2
}

public enum ImageExportFormat
{
    Png = 0,
    Jpg = 1
}

public enum AppThemeMode
{
    Light = 0,
    Dark = 1
}

public class ShiftTimeRange : INotifyPropertyChanged
{
    private int _startHour;
    private int _startMinute;
    private int _endHour;
    private int _endMinute;

    public int StartHour
    {
        get => _startHour;
        set { if (_startHour != value) { _startHour = value; OnPropertyChanged(); OnPropertyChanged(nameof(Formatted)); } }
    }

    public int StartMinute
    {
        get => _startMinute;
        set { if (_startMinute != value) { _startMinute = value; OnPropertyChanged(); OnPropertyChanged(nameof(Formatted)); } }
    }

    public int EndHour
    {
        get => _endHour;
        set { if (_endHour != value) { _endHour = value; OnPropertyChanged(); OnPropertyChanged(nameof(Formatted)); } }
    }

    public int EndMinute
    {
        get => _endMinute;
        set { if (_endMinute != value) { _endMinute = value; OnPropertyChanged(); OnPropertyChanged(nameof(Formatted)); } }
    }

    [JsonIgnore]
    public string Formatted => $"{StartHour:00}:{StartMinute:00} - {EndHour:00}:{EndMinute:00}";

    public ShiftTimeRange Clone() => new()
    {
        StartHour = StartHour,
        StartMinute = StartMinute,
        EndHour = EndHour,
        EndMinute = EndMinute
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class PersonConfig : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private LeaveMode _leaveMode = LeaveMode.Rastgele;
    private int _selectedDayIndex = 0; // 0=Pazartesi .. 6=Pazar

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    public LeaveMode LeaveMode
    {
        get => _leaveMode;
        set
        {
            if (_leaveMode != value)
            {
                _leaveMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDaySelectorEnabled));
                OnPropertyChanged(nameof(LeaveSummaryText));
            }
        }
    }

    /// <summary>
    /// 0 = Pazartesi, 1 = Salı, ..., 6 = Pazar
    /// </summary>
    public int SelectedDayIndex
    {
        get => _selectedDayIndex;
        set
        {
            if (_selectedDayIndex != value)
            {
                _selectedDayIndex = Math.Clamp(value, 0, 6);
                OnPropertyChanged();
                OnPropertyChanged(nameof(LeaveSummaryText));
            }
        }
    }

    [JsonIgnore]
    public bool IsDaySelectorEnabled => LeaveMode == LeaveMode.BelirliGun;

    [JsonIgnore]
    public string LeaveSummaryText => LeaveMode switch
    {
        LeaveMode.Rastgele => "Rastgele İzin",
        LeaveMode.IzinYok => "İzin Yok",
        LeaveMode.BelirliGun => DayNamesTr[Math.Clamp(SelectedDayIndex, 0, 6)],
        _ => "Rastgele İzin"
    };

    public static readonly string[] DayNamesTr =
    {
        "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar"
    };

    public PersonConfig Clone() => new()
    {
        Id = Id,
        Name = Name,
        LeaveMode = LeaveMode,
        SelectedDayIndex = SelectedDayIndex
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class CustomizationSettings : INotifyPropertyChanged
{
    private AppThemeMode _themeMode = AppThemeMode.Dark;

    private bool _enableCustomHours = true;
    private ShiftTimeRange _sabahTime = new() { StartHour = 9, StartMinute = 0, EndHour = 18, EndMinute = 0 };
    private ShiftTimeRange _araTime = new() { StartHour = 11, StartMinute = 0, EndHour = 20, EndMinute = 0 };
    private ShiftTimeRange _aksamTime = new() { StartHour = 13, StartMinute = 0, EndHour = 22, EndMinute = 0 };
    private ShiftTimeRange _fullTime = new() { StartHour = 9, StartMinute = 0, EndHour = 22, EndMinute = 0 };

    private bool _enableLeaveCustomization = true;
    private bool _bulkSundayLeave = false;
    private bool _noLeaveForAll = false;
    private RandomLeaveScope _randomLeaveScope = RandomLeaveScope.HaftaIciPztCum;
    private bool _allowLeaveOverlap = false;

    private bool _enableFullShifts = false;
    // Day index (0..6) -> PersonId (or null/empty if none selected)
    private List<string?> _fullPersonIdByDay = new() { null, null, null, null, null, null, null };

    private bool _enableDistributionCustomization = false;
    private int _dailySabahCount = 1;
    private int _dailyAraCount = 1;
    private bool _twoWorkerPreferAksam = true;

    private bool _enableRuleBeforeAfterLeave = true;
    private bool _enableFairEarlyDistribution = true;
    private bool _enableHistoryComparison = true;
    private bool _allowHistoryRelaxation = true;

    private bool _enableExportCustomization = true;
    private ImageExportFormat _exportFormat = ImageExportFormat.Png;
    private bool _showHoursInTable = true;
    private bool _showSummaryColumns = false;

    public AppThemeMode ThemeMode
    {
        get => _themeMode;
        set { if (_themeMode != value) { _themeMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDarkTheme)); } }
    }

    [JsonIgnore]
    public bool IsDarkTheme
    {
        get => _themeMode == AppThemeMode.Dark;
        set => ThemeMode = value ? AppThemeMode.Dark : AppThemeMode.Light;
    }

    public bool EnableCustomHours
    {
        get => _enableCustomHours;
        set { _enableCustomHours = value; OnPropertyChanged(); }
    }

    public ShiftTimeRange SabahTime
    {
        get => _sabahTime;
        set { _sabahTime = value; OnPropertyChanged(); }
    }

    public ShiftTimeRange AraTime
    {
        get => _araTime;
        set { _araTime = value; OnPropertyChanged(); }
    }

    public ShiftTimeRange AksamTime
    {
        get => _aksamTime;
        set { _aksamTime = value; OnPropertyChanged(); }
    }

    public ShiftTimeRange FullTime
    {
        get => _fullTime;
        set { _fullTime = value; OnPropertyChanged(); }
    }

    public bool EnableLeaveCustomization
    {
        get => _enableLeaveCustomization;
        set { _enableLeaveCustomization = value; OnPropertyChanged(); }
    }

    public bool BulkSundayLeave
    {
        get => _bulkSundayLeave;
        set
        {
            if (_bulkSundayLeave != value)
            {
                _bulkSundayLeave = value;
                if (value) _noLeaveForAll = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NoLeaveForAll));
            }
        }
    }

    public bool NoLeaveForAll
    {
        get => _noLeaveForAll;
        set
        {
            if (_noLeaveForAll != value)
            {
                _noLeaveForAll = value;
                if (value) _bulkSundayLeave = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BulkSundayLeave));
            }
        }
    }

    public RandomLeaveScope RandomLeaveScope
    {
        get => _randomLeaveScope;
        set { _randomLeaveScope = value; OnPropertyChanged(); }
    }

    public bool AllowLeaveOverlap
    {
        get => _allowLeaveOverlap;
        set { _allowLeaveOverlap = value; OnPropertyChanged(); }
    }

    public bool EnableFullShifts
    {
        get => _enableFullShifts;
        set { if (_enableFullShifts != value) { _enableFullShifts = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// 7 elemanlı liste (0=Pazartesi .. 6=Pazar): o gün Full kalacak kişinin Id'si, yoksa null.
    /// </summary>
    public List<string?> FullPersonIdByDay
    {
        get => _fullPersonIdByDay;
        set { _fullPersonIdByDay = NormalizeFullList(value); OnPropertyChanged(); }
    }

    public static List<string?> NormalizeFullList(List<string?>? source)
    {
        var list = new List<string?>(7);
        for (int i = 0; i < 7; i++)
            list.Add(source != null && i < source.Count && !string.IsNullOrWhiteSpace(source[i]) ? source[i] : null);
        return list;
    }

    public bool EnableDistributionCustomization
    {
        get => _enableDistributionCustomization;
        set { _enableDistributionCustomization = value; OnPropertyChanged(); }
    }

    public int DailySabahCount
    {
        get => _dailySabahCount;
        set { _dailySabahCount = Math.Clamp(value, 1, 4); OnPropertyChanged(); }
    }

    public int DailyAraCount
    {
        get => _dailyAraCount;
        set { _dailyAraCount = Math.Clamp(value, 0, 4); OnPropertyChanged(); }
    }

    public bool TwoWorkerPreferAksam
    {
        get => _twoWorkerPreferAksam;
        set { _twoWorkerPreferAksam = value; OnPropertyChanged(); }
    }

    public bool EnableRuleBeforeAfterLeave
    {
        get => _enableRuleBeforeAfterLeave;
        set { _enableRuleBeforeAfterLeave = value; OnPropertyChanged(); }
    }

    public bool EnableFairEarlyDistribution
    {
        get => _enableFairEarlyDistribution;
        set { _enableFairEarlyDistribution = value; OnPropertyChanged(); }
    }

    public bool EnableHistoryComparison
    {
        get => _enableHistoryComparison;
        set { _enableHistoryComparison = value; OnPropertyChanged(); }
    }

    public bool AllowHistoryRelaxation
    {
        get => _allowHistoryRelaxation;
        set { _allowHistoryRelaxation = value; OnPropertyChanged(); }
    }

    public bool EnableExportCustomization
    {
        get => _enableExportCustomization;
        set { _enableExportCustomization = value; OnPropertyChanged(); }
    }

    public ImageExportFormat ExportFormat
    {
        get => _exportFormat;
        set { _exportFormat = value; OnPropertyChanged(); }
    }

    public bool ShowHoursInTable
    {
        get => _showHoursInTable;
        set { _showHoursInTable = value; OnPropertyChanged(); }
    }

    public bool ShowSummaryColumns
    {
        get => _showSummaryColumns;
        set { _showSummaryColumns = value; OnPropertyChanged(); }
    }

    public CustomizationSettings Clone() => new()
    {
        ThemeMode = ThemeMode,
        EnableCustomHours = EnableCustomHours,
        SabahTime = SabahTime.Clone(),
        AraTime = AraTime.Clone(),
        AksamTime = AksamTime.Clone(),
        FullTime = FullTime.Clone(),
        EnableLeaveCustomization = EnableLeaveCustomization,
        BulkSundayLeave = BulkSundayLeave,
        NoLeaveForAll = NoLeaveForAll,
        RandomLeaveScope = RandomLeaveScope,
        AllowLeaveOverlap = AllowLeaveOverlap,
        EnableFullShifts = EnableFullShifts,
        FullPersonIdByDay = NormalizeFullList(FullPersonIdByDay),
        EnableDistributionCustomization = EnableDistributionCustomization,
        DailySabahCount = DailySabahCount,
        DailyAraCount = DailyAraCount,
        TwoWorkerPreferAksam = TwoWorkerPreferAksam,
        EnableRuleBeforeAfterLeave = EnableRuleBeforeAfterLeave,
        EnableFairEarlyDistribution = EnableFairEarlyDistribution,
        EnableHistoryComparison = EnableHistoryComparison,
        AllowHistoryRelaxation = AllowHistoryRelaxation,
        EnableExportCustomization = EnableExportCustomization,
        ExportFormat = ExportFormat,
        ShowHoursInTable = ShowHoursInTable,
        ShowSummaryColumns = ShowSummaryColumns
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class PersonWeekSchedule
{
    public string PersonName { get; set; } = string.Empty;
    public ShiftType[] Days { get; set; } = new ShiftType[7];
    public int SabahCount { get; set; }
    public int AraCount { get; set; }
    public int EarlyTotal => SabahCount + AraCount;
    public int AksamCount { get; set; }
    public int FullCount { get; set; }
    public int IzinCount { get; set; }
}

public class WeeklySchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime WeekStartMonday { get; set; }
    public DateTime WeekEndSunday { get; set; }
    public string WeekDisplayLabel { get; set; } = string.Empty;
    public string FileBaseName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ShiftTimeRange SabahTime { get; set; } = new();
    public ShiftTimeRange AraTime { get; set; } = new();
    public ShiftTimeRange AksamTime { get; set; } = new();
    public ShiftTimeRange FullTime { get; set; } = new() { StartHour = 9, StartMinute = 0, EndHour = 22, EndMinute = 0 };
    public List<PersonWeekSchedule> Rows { get; set; } = new();
    public List<string> AppliedRules { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? ImageFilePath { get; set; }
    public bool ShowHoursInTable { get; set; } = true;
    public bool ShowSummaryColumns { get; set; } = false;
    public bool HasFullShifts { get; set; }
    public int ComparedHistoryWeeksCount { get; set; }
    public int SimilarityOverlapCount { get; set; }
}

public class AppState
{
    public string SaveFolderPath { get; set; } = string.Empty;
    public bool HasUserChosenSaveFolder { get; set; }
    public DateTime TargetMondayDate { get; set; }
    public List<PersonConfig> People { get; set; } = new();
    public CustomizationSettings Settings { get; set; } = new();
    public WeeklySchedule? CurrentSchedule { get; set; }
    public DateTime LastSavedAt { get; set; } = DateTime.Now;
}
