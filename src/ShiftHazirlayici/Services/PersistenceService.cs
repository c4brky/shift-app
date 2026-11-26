// Hey! Sonunda beni bulabildin, seni tebrik ediyorum. <3
// made by c4brky
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShiftHazirlayici.Models;

namespace ShiftHazirlayici.Services;

public class PersistenceService
{
    private readonly string _appDataRoot;
    private readonly string _autoSaveFilePath;
    private readonly string _autoSaveTempPath;
    private readonly string _historyFolderPath;
    private readonly string _historyIndexPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PersistenceService(string? customRootDirectory = null)
    {
        _appDataRoot = customRootDirectory ??
                       Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShiftHazirlayici");
        Directory.CreateDirectory(_appDataRoot);

        _autoSaveFilePath = Path.Combine(_appDataRoot, "autosave_state.json");
        _autoSaveTempPath = Path.Combine(_appDataRoot, "autosave_state.tmp");
        _historyFolderPath = Path.Combine(_appDataRoot, "History");
        Directory.CreateDirectory(_historyFolderPath);
        _historyIndexPath = Path.Combine(_historyFolderPath, "history_index.json");
    }

    public string AutoSaveFilePath => _autoSaveFilePath;

    public static string GetDefaultSaveFolder()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShiftKayitlari"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ShiftKayitlari"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShiftHazirlayici", "Kayitlar")
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith("ShiftKayitlari", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                Directory.CreateDirectory(candidate);
                return candidate;
            }
            catch
            {
                // Try next safe location
            }
        }

        string fallback = Path.Combine(AppContext.BaseDirectory, "Kayitlar");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    public AppState LoadOrCreateDefaultState()
    {
        try
        {
            if (File.Exists(_autoSaveFilePath))
            {
                string json = File.ReadAllText(_autoSaveFilePath);
                var loaded = JsonSerializer.Deserialize<AppState>(json, JsonOptions);
                if (loaded != null)
                {
                    NormalizeState(loaded);
                    return loaded;
                }
            }
        }
        catch
        {
            // Fall back to temp backup if main file was interrupted
            try
            {
                if (File.Exists(_autoSaveTempPath))
                {
                    string json = File.ReadAllText(_autoSaveTempPath);
                    var loaded = JsonSerializer.Deserialize<AppState>(json, JsonOptions);
                    if (loaded != null)
                    {
                        NormalizeState(loaded);
                        return loaded;
                    }
                }
            }
            catch
            {
                // Ignore corrupted fallback and return fresh defaults
            }
        }

        return CreateDefaultState();
    }

    private static void NormalizeState(AppState state)
    {
        if (string.IsNullOrWhiteSpace(state.SaveFolderPath))
            state.SaveFolderPath = GetDefaultSaveFolder();

        if (state.TargetMondayDate == default)
            state.TargetMondayDate = WeekDateHelper.GetMonday(DateTime.Today);
        else
            state.TargetMondayDate = WeekDateHelper.GetMonday(state.TargetMondayDate);

        state.Settings ??= new CustomizationSettings();
        state.Settings.SabahTime ??= new ShiftTimeRange { StartHour = 9, StartMinute = 0, EndHour = 18, EndMinute = 0 };
        state.Settings.AraTime ??= new ShiftTimeRange { StartHour = 11, StartMinute = 0, EndHour = 20, EndMinute = 0 };
        state.Settings.AksamTime ??= new ShiftTimeRange { StartHour = 13, StartMinute = 0, EndHour = 22, EndMinute = 0 };
        state.Settings.FullTime ??= new ShiftTimeRange { StartHour = 9, StartMinute = 0, EndHour = 22, EndMinute = 0 };
        state.Settings.FullPersonIdByDay = CustomizationSettings.NormalizeFullList(state.Settings.FullPersonIdByDay);

        state.People ??= new List<PersonConfig>();
        while (state.People.Count < 3)
        {
            state.People.Add(new PersonConfig { Name = $"Personel {state.People.Count + 1}" });
        }
        if (state.People.Count > 10)
        {
            state.People = state.People.Take(10).ToList();
        }
    }

    public static AppState CreateDefaultState()
    {
        return new AppState
        {
            SaveFolderPath = GetDefaultSaveFolder(),
            TargetMondayDate = WeekDateHelper.GetMonday(DateTime.Today),
            People = new List<PersonConfig>
            {
                new() { Name = "Personel 1", LeaveMode = LeaveMode.Rastgele },
                new() { Name = "Personel 2", LeaveMode = LeaveMode.Rastgele },
                new() { Name = "Personel 3", LeaveMode = LeaveMode.Rastgele },
                new() { Name = "Personel 4", LeaveMode = LeaveMode.Rastgele },
                new() { Name = "Personel 5", LeaveMode = LeaveMode.Rastgele }
            },
            Settings = new CustomizationSettings(),
            LastSavedAt = DateTime.Now
        };
    }

    public void SaveAutoState(AppState state)
    {
        state.LastSavedAt = DateTime.Now;
        string json = JsonSerializer.Serialize(state, JsonOptions);

        Directory.CreateDirectory(_appDataRoot);
        File.WriteAllText(_autoSaveTempPath, json);

        if (File.Exists(_autoSaveFilePath))
        {
            File.Replace(_autoSaveTempPath, _autoSaveFilePath, null);
        }
        else
        {
            File.Move(_autoSaveTempPath, _autoSaveFilePath);
        }
    }

    public List<WeeklySchedule> LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyIndexPath))
                return new List<WeeklySchedule>();

            string json = File.ReadAllText(_historyIndexPath);
            var list = JsonSerializer.Deserialize<List<WeeklySchedule>>(json, JsonOptions);
            return list?
                .OrderByDescending(s => s.CreatedAt)
                .ToList() ?? new List<WeeklySchedule>();
        }
        catch
        {
            return new List<WeeklySchedule>();
        }
    }

    public void SaveToHistory(WeeklySchedule schedule)
    {
        var list = LoadHistory();
        list.RemoveAll(x => x.Id == schedule.Id);
        list.Insert(0, schedule);

        SaveHistoryList(list);

        // Also store individual snapshot file
        string snapshotPath = Path.Combine(_historyFolderPath, $"{schedule.FileBaseName}_{schedule.Id[..6]}.json");
        File.WriteAllText(snapshotPath, JsonSerializer.Serialize(schedule, JsonOptions));
    }

    public void DeleteFromHistory(string scheduleId)
    {
        var list = LoadHistory();
        list.RemoveAll(x => x.Id == scheduleId);
        SaveHistoryList(list);
    }

    private void SaveHistoryList(List<WeeklySchedule> list)
    {
        Directory.CreateDirectory(_historyFolderPath);
        string tmp = Path.Combine(_historyFolderPath, "history_index.tmp");
        File.WriteAllText(tmp, JsonSerializer.Serialize(list, JsonOptions));
        if (File.Exists(_historyIndexPath))
            File.Replace(tmp, _historyIndexPath, null);
        else
            File.Move(tmp, _historyIndexPath);
    }
}
