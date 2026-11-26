// created by c4brky
using System;
using System.Collections.Generic;
using System.Windows.Media;
using ShiftMaker.Models;

namespace ShiftMaker.Services;

public sealed record ShiftColorPreset(
    string Key,
    string NameTr,
    string NameEn,
    Color Bg,
    Color Border,
    Color Fg);

public static class ShiftColorPalette
{
    public static readonly IReadOnlyList<ShiftColorPreset> Presets = new List<ShiftColorPreset>
    {
        new("blue", "Mavi", "Blue", Color.FromRgb(20, 37, 59), Color.FromRgb(37, 99, 235), Color.FromRgb(125, 211, 252)),
        new("amber", "Kehribar", "Amber", Color.FromRgb(46, 37, 19), Color.FromRgb(146, 107, 29), Color.FromRgb(251, 191, 36)),
        new("slate", "Antrasit Gri", "Slate Gray", Color.FromRgb(44, 48, 56), Color.FromRgb(82, 88, 102), Color.FromRgb(209, 213, 219)),
        new("emerald", "Zümrüt", "Emerald", Color.FromRgb(18, 43, 34), Color.FromRgb(16, 185, 129), Color.FromRgb(110, 231, 183)),
        new("rose", "Gül", "Rose", Color.FromRgb(53, 22, 33), Color.FromRgb(225, 29, 72), Color.FromRgb(253, 164, 175)),
        new("violet", "Mor", "Violet", Color.FromRgb(41, 27, 62), Color.FromRgb(124, 58, 237), Color.FromRgb(196, 181, 253)),
        new("cyan", "Camgöbeği", "Cyan", Color.FromRgb(17, 45, 54), Color.FromRgb(8, 145, 178), Color.FromRgb(103, 232, 249)),
        new("orange", "Turuncu", "Orange", Color.FromRgb(56, 32, 16), Color.FromRgb(234, 88, 12), Color.FromRgb(253, 186, 116)),
        new("teal", "Turkuaz", "Teal", Color.FromRgb(17, 44, 43), Color.FromRgb(13, 148, 136), Color.FromRgb(94, 234, 212)),
        new("indigo", "Çivit", "Indigo", Color.FromRgb(27, 30, 62), Color.FromRgb(79, 70, 229), Color.FromRgb(165, 180, 252)),
        new("pink", "Pembe", "Pink", Color.FromRgb(56, 22, 46), Color.FromRgb(219, 39, 119), Color.FromRgb(249, 168, 212)),
        new("lime", "Fıstık Yeşili", "Lime", Color.FromRgb(30, 46, 18), Color.FromRgb(101, 163, 13), Color.FromRgb(190, 242, 100)),
        new("red", "Kırmızı", "Red", Color.FromRgb(56, 22, 22), Color.FromRgb(220, 38, 38), Color.FromRgb(252, 165, 165)),
        new("gold", "Altın", "Gold", Color.FromRgb(48, 41, 18), Color.FromRgb(202, 138, 4), Color.FromRgb(253, 224, 71))
    };

    public static string GetDefaultKey(ShiftType shiftType) => shiftType switch
    {
        ShiftType.Morning => "blue",
        ShiftType.Mid => "amber",
        ShiftType.Evening => "slate",
        ShiftType.Off => "emerald",
        ShiftType.Full => "rose",
        _ => "slate"
    };

    public static ShiftColorPreset GetPreset(string? key, ShiftType fallbackType)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var preset in Presets)
            {
                if (string.Equals(preset.Key, key, StringComparison.OrdinalIgnoreCase))
                    return preset;
            }
        }

        string defKey = GetDefaultKey(fallbackType);
        foreach (var preset in Presets)
        {
            if (string.Equals(preset.Key, defKey, StringComparison.OrdinalIgnoreCase))
                return preset;
        }

        return Presets[0];
    }

    public static string GetKeyForShift(ShiftType shiftType, CustomizationSettings? settings)
    {
        if (settings == null) return GetDefaultKey(shiftType);
        return shiftType switch
        {
            ShiftType.Morning => settings.MorningColorKey,
            ShiftType.Mid => settings.MidColorKey,
            ShiftType.Evening => settings.EveningColorKey,
            ShiftType.Off => settings.OffColorKey,
            ShiftType.Full => settings.FullColorKey,
            _ => GetDefaultKey(shiftType)
        };
    }

    public static string GetKeyForShift(ShiftType shiftType, WeeklySchedule? schedule)
    {
        if (schedule == null) return GetDefaultKey(shiftType);
        return shiftType switch
        {
            ShiftType.Morning => schedule.MorningColorKey,
            ShiftType.Mid => schedule.MidColorKey,
            ShiftType.Evening => schedule.EveningColorKey,
            ShiftType.Off => schedule.OffColorKey,
            ShiftType.Full => schedule.FullColorKey,
            _ => GetDefaultKey(shiftType)
        };
    }
}
