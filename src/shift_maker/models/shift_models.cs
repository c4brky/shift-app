// created by c4brky
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ShiftMaker.Models;

public enum ShiftType
{
    Morning = 0,
    Mid = 1,
    Evening = 2,
    Off = 3,
    Full = 4
}

public enum LeaveMode
{
    Random = 0,
    FixedDay = 1,
    NoLeave = 2
}

public enum RandomLeaveScope
{
    FirstFiveDays = 0,
    FirstSixDays = 1,
    FullWeek = 2
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

public enum AppLanguage
{
    Tr = 0,
    En = 1
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
    private LeaveMode _leaveMode = LeaveMode.Random;
    private int _selectedDayIndex = 0; // 0..6 relative to week start date

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
            }
        }
    }

    /// <summary>
    /// Day offset (0..6) relative to the selected week start date.
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
            }
        }
    }

    [JsonIgnore]
    public bool IsDaySelectorEnabled => LeaveMode == LeaveMode.FixedDay;

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
    private AppLanguage _language = AppLanguage.Tr;

    private string _morningColorKey = "blue";
    private string _midColorKey = "amber";
    private string _eveningColorKey = "slate";
    private string _fullColorKey = "rose";
    private string _offColorKey = "emerald";

    private bool _enableCustomHours = true;
    private ShiftTimeRange _morningTime = new() { StartHour = 9, StartMinute = 0, EndHour = 18, EndMinute = 0 };
    private ShiftTimeRange _midTime = new() { StartHour = 11, StartMinute = 0, EndHour = 20, EndMinute = 0 };
    private ShiftTimeRange _eveningTime = new() { StartHour = 13, StartMinute = 0, EndHour = 22, EndMinute = 0 };
    private ShiftTimeRange _fullTime = new() { StartHour = 9, StartMinute = 0, EndHour = 22, EndMinute = 0 };

    private bool _enableLeaveCustomization = true;
    private bool _bulkLastDayLeave = false;
    private bool _noLeaveForAll = false;
    private RandomLeaveScope _randomLeaveScope = RandomLeaveScope.FirstFiveDays;
    private bool _allowLeaveOverlap = false;

    private bool _enableFullShifts = false;
    private List<string?> _fullPersonIdByDay = new() { null, null, null, null, null, null, null };

    private bool _enableDistributionCustomization = false;
    private int _dailyMorningCount = 1;
    private int _dailyMidCount = 1;
    private bool _twoWorkerPreferEvening = true;

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

    public AppLanguage Language
    {
        get => _language;
        set { if (_language != value) { _language = value; OnPropertyChanged(); } }
    }

    public string MorningColorKey
    {
        get => _morningColorKey;
        set { if (_morningColorKey != value) { _morningColorKey = value; OnPropertyChanged(); } }
    }

    public string MidColorKey
    {
        get => _midColorKey;
        set { if (_midColorKey != value) { _midColorKey = value; OnPropertyChanged(); } }
    }

    public string EveningColorKey
    {
        get => _eveningColorKey;
        set { if (_eveningColorKey != value) { _eveningColorKey = value; OnPropertyChanged(); } }
    }

    public string FullColorKey
    {
        get => _fullColorKey;
        set { if (_fullColorKey != value) { _fullColorKey = value; OnPropertyChanged(); } }
    }

    public string OffColorKey
    {
        get => _offColorKey;
        set { if (_offColorKey != value) { _offColorKey = value; OnPropertyChanged(); } }
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

    public ShiftTimeRange MorningTime
    {
        get => _morningTime;
        set { _morningTime = value; OnPropertyChanged(); }
    }

    public ShiftTimeRange MidTime
    {
        get => _midTime;
        set { _midTime = value; OnPropertyChanged(); }
    }

    public ShiftTimeRange EveningTime
    {
        get => _eveningTime;
        set { _eveningTime = value; OnPropertyChanged(); }
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

    public bool BulkLastDayLeave
    {
        get => _bulkLastDayLeave;
        set
        {
            if (_bulkLastDayLeave != value)
            {
                _bulkLastDayLeave = value;
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
                if (value) _bulkLastDayLeave = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BulkLastDayLeave));
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

    public int DailyMorningCount
    {
        get => _dailyMorningCount;
        set { _dailyMorningCount = Math.Clamp(value, 1, 30); OnPropertyChanged(); }
    }

    public int DailyMidCount
    {
        get => _dailyMidCount;
        set { _dailyMidCount = Math.Clamp(value, 0, 30); OnPropertyChanged(); }
    }

    public bool TwoWorkerPreferEvening
    {
        get => _twoWorkerPreferEvening;
        set { _twoWorkerPreferEvening = value; OnPropertyChanged(); }
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
        Language = Language,
        MorningColorKey = MorningColorKey,
        MidColorKey = MidColorKey,
        EveningColorKey = EveningColorKey,
        FullColorKey = FullColorKey,
        OffColorKey = OffColorKey,
        EnableCustomHours = EnableCustomHours,
        MorningTime = MorningTime.Clone(),
        MidTime = MidTime.Clone(),
        EveningTime = EveningTime.Clone(),
        FullTime = FullTime.Clone(),
        EnableLeaveCustomization = EnableLeaveCustomization,
        BulkLastDayLeave = BulkLastDayLeave,
        NoLeaveForAll = NoLeaveForAll,
        RandomLeaveScope = RandomLeaveScope,
        AllowLeaveOverlap = AllowLeaveOverlap,
        EnableFullShifts = EnableFullShifts,
        FullPersonIdByDay = NormalizeFullList(FullPersonIdByDay),
        EnableDistributionCustomization = EnableDistributionCustomization,
        DailyMorningCount = DailyMorningCount,
        DailyMidCount = DailyMidCount,
        TwoWorkerPreferEvening = TwoWorkerPreferEvening,
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
    public int MorningCount { get; set; }
    public int MidCount { get; set; }
    public int EarlyTotal => MorningCount + MidCount;
    public int EveningCount { get; set; }
    public int FullCount { get; set; }
    public int OffCount { get; set; }
}

public class WeeklySchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime WeekStartDate { get; set; }
    public DateTime WeekEndDate { get; set; }
    public string WeekDisplayLabel { get; set; } = string.Empty;
    public string FileBaseName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ShiftTimeRange MorningTime { get; set; } = new();
    public ShiftTimeRange MidTime { get; set; } = new();
    public ShiftTimeRange EveningTime { get; set; } = new();
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
    public AppLanguage Language { get; set; } = AppLanguage.Tr;
    public string MorningColorKey { get; set; } = "blue";
    public string MidColorKey { get; set; } = "amber";
    public string EveningColorKey { get; set; } = "slate";
    public string FullColorKey { get; set; } = "rose";
    public string OffColorKey { get; set; } = "emerald";
}

public class AppState
{
    public string SaveFolderPath { get; set; } = string.Empty;
    public bool HasUserChosenSaveFolder { get; set; }
    public DateTime TargetStartDate { get; set; }
    public List<PersonConfig> People { get; set; } = new();
    public CustomizationSettings Settings { get; set; } = new();
    public WeeklySchedule? CurrentSchedule { get; set; }
    public DateTime LastSavedAt { get; set; } = DateTime.Now;
}
