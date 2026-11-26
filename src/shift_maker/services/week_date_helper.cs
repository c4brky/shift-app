// made by c4brky
using System;
using System.IO;
using System.Linq;
using ShiftMaker.Models;

namespace ShiftMaker.Services;

public static class WeekDateHelper
{
    public static readonly int[] ValidHours = Enumerable.Range(7, 16).ToArray(); // 07..22
    public static readonly int[] ValidMinutes = { 0, 15, 30, 45 };

    public static DateTime GetMonday(DateTime date)
    {
        var d = date.Date;
        int diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
        return d.AddDays(-diff);
    }

    public static DateTime GetWeekEnd(DateTime startDate) => startDate.Date.AddDays(6);

    /// <summary>
    /// Formats week label e.g. "2026 - 11.23/11.29" or "2026 - 11.30/12.06".
    /// </summary>
    public static string GetWeekDisplayLabel(DateTime startDate)
    {
        var start = startDate.Date;
        var end = start.AddDays(6);

        if (start.Year == end.Year)
            return $"{start:yyyy} - {start:MM}.{start:dd}/{end:MM}.{end:dd}";

        return $"{start:yyyy}.{start:MM}.{start:dd}/{end:yyyy}.{end:MM}.{end:dd}";
    }

    /// <summary>
    /// Formats a Windows-safe file base name e.g. "2026 - 11.23-11.29".
    /// </summary>
    public static string GetSafeFileBaseName(DateTime startDate)
    {
        var label = GetWeekDisplayLabel(startDate).Replace('/', '-');
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            label = label.Replace(c, '-');
        }
        return label;
    }

    public static string GetDayName(DateTime startDate, int dayOffset, AppLanguage lang = AppLanguage.Tr)
    {
        var date = startDate.Date.AddDays(dayOffset);
        return LocalizationService.GetDayName(date.DayOfWeek, lang);
    }

    public static string GetDayHeaderLabel(DateTime startDate, int dayOffset, AppLanguage lang = AppLanguage.Tr)
    {
        var date = startDate.Date.AddDays(dayOffset);
        string dayName = LocalizationService.GetDayName(date.DayOfWeek, lang);
        return $"{dayName}\n{date:dd.MM}";
    }
}
