// created by c4brky
using System;
using ShiftMaker.Models;

namespace ShiftMaker.Services;

public static class LocalizationService
{
    public static bool IsEnglish(AppLanguage lang) => lang == AppLanguage.En;

    public static string T(AppLanguage lang, string tr, string en) =>
        lang == AppLanguage.En ? en : tr;

    public static string GetDayName(DayOfWeek dayOfWeek, AppLanguage lang)
    {
        return (dayOfWeek, lang) switch
        {
            (DayOfWeek.Monday, AppLanguage.En) => "Monday",
            (DayOfWeek.Tuesday, AppLanguage.En) => "Tuesday",
            (DayOfWeek.Wednesday, AppLanguage.En) => "Wednesday",
            (DayOfWeek.Thursday, AppLanguage.En) => "Thursday",
            (DayOfWeek.Friday, AppLanguage.En) => "Friday",
            (DayOfWeek.Saturday, AppLanguage.En) => "Saturday",
            (DayOfWeek.Sunday, AppLanguage.En) => "Sunday",

            (DayOfWeek.Monday, _) => "Pazartesi",
            (DayOfWeek.Tuesday, _) => "Salı",
            (DayOfWeek.Wednesday, _) => "Çarşamba",
            (DayOfWeek.Thursday, _) => "Perşembe",
            (DayOfWeek.Friday, _) => "Cuma",
            (DayOfWeek.Saturday, _) => "Cumartesi",
            _ => "Pazar"
        };
    }

    public static string GetShiftLabel(ShiftType st, AppLanguage lang, bool uppercase = true)
    {
        return (st, lang) switch
        {
            (ShiftType.Morning, AppLanguage.En) => uppercase ? "MORNING" : "Morning",
            (ShiftType.Mid, AppLanguage.En) => uppercase ? "MID SHIFT" : "Mid Shift",
            (ShiftType.Evening, AppLanguage.En) => uppercase ? "EVENING" : "Evening",
            (ShiftType.Off, AppLanguage.En) => uppercase ? "OFF" : "Day Off",
            (ShiftType.Full, AppLanguage.En) => "FULL",

            (ShiftType.Morning, _) => uppercase ? "SABAHÇI" : "Sabahçı",
            (ShiftType.Mid, _) => uppercase ? "ARA SHIFT" : "Ara Shift",
            (ShiftType.Evening, _) => uppercase ? "AKŞAMCI" : "Akşamcı",
            (ShiftType.Off, _) => uppercase ? "İZİN" : "İzin",
            (ShiftType.Full, _) => uppercase ? "FULL" : "Full",
            _ => "-"
        };
    }

    public static string GetLeaveSubLabel(AppLanguage lang) =>
        lang == AppLanguage.En ? "Rest Day" : "Dinlenme";
}
