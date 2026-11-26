// Hey! Sonunda beni bulabildin, seni tebrik ediyorum. <3
// made by c4brky
using System;
using System.IO;
using System.Linq;

namespace ShiftHazirlayici.Services;

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

    public static DateTime GetSunday(DateTime monday) => GetMonday(monday).AddDays(6);

    /// <summary>
    /// Formats week label e.g. "2026.11 - 23/29" for same month, or cross-month/year equivalents.
    /// </summary>
    public static string GetWeekDisplayLabel(DateTime anyDate)
    {
        var mon = GetMonday(anyDate);
        var sun = mon.AddDays(6);

        if (mon.Year == sun.Year && mon.Month == sun.Month)
            return $"{mon:yyyy.MM} - {mon:dd}/{sun:dd}";

        if (mon.Year == sun.Year)
            return $"{mon:yyyy.MM}.{mon:dd}/{sun:MM}.{sun:dd}";

        return $"{mon:yyyy.MM.dd} - {sun:yyyy.MM.dd}";
    }

    /// <summary>
    /// Formats a Windows-safe file base name e.g. "2026.11 - 23-29".
    /// </summary>
    public static string GetSafeFileBaseName(DateTime anyDate)
    {
        var label = GetWeekDisplayLabel(anyDate).Replace('/', '-');
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            label = label.Replace(c, '-');
        }
        return label;
    }

    public static string GetDayHeaderLabel(DateTime monday, int dayIndex)
    {
        var dayNames = new[] { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };
        var date = GetMonday(monday).AddDays(dayIndex);
        return $"{dayNames[dayIndex]}\n{date:dd.MM}";
    }
}
