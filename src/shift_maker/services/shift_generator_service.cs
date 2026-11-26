// created by c4brky
using System;
using System.Collections.Generic;
using System.Linq;
using ShiftMaker.Models;

namespace ShiftMaker.Services;

public class ShiftGenerationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public WeeklySchedule? Schedule { get; set; }
}

public class ShiftGeneratorService
{
    public ShiftGenerationResult Generate(
        IReadOnlyList<PersonConfig> people,
        DateTime targetDate,
        CustomizationSettings settings,
        IReadOnlyList<WeeklySchedule> recentSchedules,
        int? seed = null)
    {
        var lang = settings.Language;
        var weekStart = targetDate.Date;
        var weekEnd = weekStart.AddDays(6);

        if (people == null || people.Count < 3 || people.Count > 30)
        {
            return new ShiftGenerationResult
            {
                Success = false,
                ErrorMessage = LocalizationService.T(
                    lang,
                    "Personel sayısı en az 3, en fazla 30 kişi olmalıdır.",
                    "Staff count must be between 3 and 30.")
            };
        }

        var trimmedNames = people.Select(p => (p.Name ?? string.Empty).Trim()).ToList();
        if (trimmedNames.Any(string.IsNullOrWhiteSpace))
        {
            return new ShiftGenerationResult
            {
                Success = false,
                ErrorMessage = LocalizationService.T(
                    lang,
                    "Lütfen shift oluşturulacak tüm kişilerin isimlerini doldurun.",
                    "Please fill in names for all staff members.")
            };
        }

        var duplicateName = trimmedNames
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateName != null)
        {
            return new ShiftGenerationResult
            {
                Success = false,
                ErrorMessage = LocalizationService.T(
                    lang,
                    $"Aynı isim birden fazla kez eklenmiş: \"{duplicateName.Key}\". Lütfen kişilere ayırt edici isimler verin.",
                    $"Duplicate name detected: \"{duplicateName.Key}\". Please use unique names.")
            };
        }

        if (!settings.BulkLastDayLeave && !settings.NoLeaveForAll && !settings.AllowLeaveOverlap)
        {
            var manualOverlap = people
                .Where(p => p.LeaveMode == LeaveMode.FixedDay)
                .GroupBy(p => p.SelectedDayIndex)
                .FirstOrDefault(g => g.Count() > 1);

            if (manualOverlap != null)
            {
                string dayName = WeekDateHelper.GetDayName(weekStart, Math.Clamp(manualOverlap.Key, 0, 6), lang);
                return new ShiftGenerationResult
                {
                    Success = false,
                    ErrorMessage = LocalizationService.T(
                        lang,
                        $"İzinler çakışamaz: Birden fazla kişiye '{dayName}' günü manuel izin atanmış. Lütfen günleri değiştirin veya Özelleştir bölümünden izin çakışmasına izin verin.",
                        $"Leave overlap is disabled: Multiple people have fixed leave on '{dayName}'. Please change days or allow overlap in Customize.")
                };
            }
        }

        var fullPersonByDay = new int?[7];
        var fullDaysForPerson = Enumerable.Range(0, people.Count).Select(_ => new HashSet<int>()).ToArray();
        bool hasAnyFull = false;

        if (settings.EnableFullShifts)
        {
            var normalizedFullIds = CustomizationSettings.NormalizeFullList(settings.FullPersonIdByDay);
            for (int d = 0; d < 7; d++)
            {
                string? targetId = normalizedFullIds[d];
                if (string.IsNullOrWhiteSpace(targetId)) continue;

                int pIdx = -1;
                for (int i = 0; i < people.Count; i++)
                {
                    if (string.Equals(people[i].Id, targetId, StringComparison.Ordinal))
                    {
                        pIdx = i;
                        break;
                    }
                }

                if (pIdx < 0) continue;

                if (!settings.NoLeaveForAll)
                {
                    string dayName = WeekDateHelper.GetDayName(weekStart, d, lang);
                    if (settings.BulkLastDayLeave && d == 6)
                    {
                        return new ShiftGenerationResult
                        {
                            Success = false,
                            ErrorMessage = LocalizationService.T(
                                lang,
                                $"Çakışma: '{trimmedNames[pIdx]}' {dayName} günü hem Toplu İzinli hem Full seçilmiş.",
                                $"Conflict: '{trimmedNames[pIdx]}' has both Bulk Leave and Full Shift on {dayName}.")
                        };
                    }

                    if (!settings.BulkLastDayLeave && people[pIdx].LeaveMode == LeaveMode.FixedDay && people[pIdx].SelectedDayIndex == d)
                    {
                        return new ShiftGenerationResult
                        {
                            Success = false,
                            ErrorMessage = LocalizationService.T(
                                lang,
                                $"Çakışma: '{trimmedNames[pIdx]}' {dayName} günü hem manuel İzinli hem Full seçilmiş.",
                                $"Conflict: '{trimmedNames[pIdx]}' has both fixed Leave and Full Shift on {dayName}.")
                        };
                    }
                }

                fullPersonByDay[d] = pIdx;
                fullDaysForPerson[pIdx].Add(d);
                hasAnyFull = true;
            }
        }

        var priorWeeks = recentSchedules
            .Where(s => s.WeekStartDate.Date < weekStart.Date)
            .OrderByDescending(s => s.WeekStartDate)
            .ThenByDescending(s => s.CreatedAt)
            .Take(2)
            .ToList();

        if (priorWeeks.Count == 0)
        {
            priorWeeks = recentSchedules
                .OrderByDescending(s => s.CreatedAt)
                .Take(2)
                .ToList();
        }

        WeeklySchedule? prev1 = priorWeeks.Count >= 1 ? priorWeeks[0] : null;
        WeeklySchedule? prev2 = priorWeeks.Count >= 2 ? priorWeeks[1] : null;

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        int n = people.Count;

        int[] candidateScopeDays = settings.RandomLeaveScope switch
        {
            RandomLeaveScope.FirstFiveDays => new[] { 0, 1, 2, 3, 4 },
            RandomLeaveScope.FirstSixDays => new[] { 0, 1, 2, 3, 4, 5 },
            _ => new[] { 0, 1, 2, 3, 4, 5, 6 }
        };

        var warnings = new List<string>();
        bool hasRandomLeave = !settings.BulkLastDayLeave && !settings.NoLeaveForAll &&
                              people.Any(p => p.LeaveMode == LeaveMode.Random);

        int leaveTrials = hasRandomLeave ? 18 : 1;
        int scheduleRestartsPerLeave = 30;

        long bestScore = long.MaxValue;
        ShiftType[,]? bestGrid = null;
        int?[]? bestLeaveDays = null;

        for (int lt = 0; lt < leaveTrials; lt++)
        {
            int?[] leaveDays = AssignLeaveDays(people, settings, candidateScopeDays, fullDaysForPerson, prev1, prev2, rng);

            var reqMorning = new int[7];
            var reqMid = new int[7];
            var reqEvening = new int[7];

            for (int d = 0; d < 7; d++)
            {
                int regularWorkers = 0;
                for (int p = 0; p < n; p++)
                {
                    if (leaveDays[p] == d) continue;
                    if (fullPersonByDay[d] == p) continue;
                    regularWorkers++;
                }

                ComputeDailyQuotas(regularWorkers, settings, out reqMorning[d], out reqMid[d], out reqEvening[d]);
            }

            for (int restart = 0; restart < scheduleRestartsPerLeave; restart++)
            {
                var grid = BuildInitialGrid(n, leaveDays, fullPersonByDay, reqMorning, reqMid, reqEvening, rng);
                OptimizeSwaps(grid, n, trimmedNames, leaveDays, settings, prev1, prev2, rng);

                long score = EvaluateScore(grid, n, trimmedNames, leaveDays, settings, prev1, prev2, out _, out _, out _);
                long jittered = score + rng.Next(0, 7);

                if (bestGrid == null || jittered < bestScore)
                {
                    bestScore = jittered;
                    bestGrid = grid;
                    bestLeaveDays = leaveDays;
                }
            }
        }

        if (bestGrid == null || bestLeaveDays == null)
        {
            return new ShiftGenerationResult
            {
                Success = false,
                ErrorMessage = LocalizationService.T(
                    lang,
                    "Shift planı oluşturulurken beklenmeyen bir hata oluştu.",
                    "An unexpected error occurred while generating the schedule.")
            };
        }

        EvaluateScore(bestGrid, n, trimmedNames, bestLeaveDays, settings, prev1, prev2,
            out int rule2Violations, out int rule4Diff, out int historyMatches);

        if (rule2Violations > 0 && settings.EnableRuleBeforeAfterLeave)
        {
            warnings.Add(LocalizationService.T(
                lang,
                $"İzin çakışması veya düşük çalışma sayısı nedeniyle 'izin öncesi sabah / sonrası akşam' kuralı {rule2Violations} noktada esnetildi.",
                $"Due to leave overlaps or low staffing, the 'morning before / evening after leave' rule was relaxed at {rule2Violations} point(s)."));
        }

        if (rule4Diff > 1 && settings.EnableFairEarlyDistribution)
        {
            warnings.Add(LocalizationService.T(
                lang,
                "Özel koşullar nedeniyle Sabah+Ara shift dağılımında 1'den fazla fark oluştu.",
                "Due to special constraints, Morning+Mid shift distribution differed by more than 1."));
        }

        if (!settings.BulkLastDayLeave && !settings.NoLeaveForAll)
        {
            var overlapDays = Enumerable.Range(0, 7)
                .Where(d => bestLeaveDays.Count(ld => ld == d) > 1)
                .ToList();
            if (overlapDays.Count > 0)
            {
                string dayList = string.Join(", ", overlapDays.Select(d => WeekDateHelper.GetDayName(weekStart, d, lang)));
                warnings.Add(LocalizationService.T(
                    lang,
                    $"Kişi sayısı ve seçilen izin kapsamı nedeniyle şu gün(ler)de birden fazla kişi izinli oldu: {dayList}.",
                    $"Due to team size and leave scope, multiple staff have leave on: {dayList}."));
            }
        }

        if (settings.EnableHistoryComparison && priorWeeks.Count > 0 && historyMatches > 0 && settings.AllowHistoryRelaxation)
        {
            int totalCells = n * 7;
            if (historyMatches > totalCells / 3)
            {
                warnings.Add(LocalizationService.T(
                    lang,
                    $"Önceki {priorWeeks.Count} haftayla tam farklılık kısıtlar nedeniyle kısmen esnetildi ({historyMatches} hücre eşleşti).",
                    $"Difference from previous {priorWeeks.Count} week(s) was partially relaxed due to constraints ({historyMatches} cells matched)."));
            }
        }

        var rows = new List<PersonWeekSchedule>();
        for (int p = 0; p < n; p++)
        {
            var row = new PersonWeekSchedule
            {
                PersonName = trimmedNames[p],
                Days = new ShiftType[7]
            };

            for (int d = 0; d < 7; d++)
            {
                var st = bestGrid[p, d];
                row.Days[d] = st;
                switch (st)
                {
                    case ShiftType.Morning: row.MorningCount++; break;
                    case ShiftType.Mid: row.MidCount++; break;
                    case ShiftType.Evening: row.EveningCount++; break;
                    case ShiftType.Off: row.OffCount++; break;
                    case ShiftType.Full: row.FullCount++; break;
                }
            }

            rows.Add(row);
        }

        var schedule = new WeeklySchedule
        {
            Id = Guid.NewGuid().ToString("N"),
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
            WeekDisplayLabel = WeekDateHelper.GetWeekDisplayLabel(weekStart),
            FileBaseName = WeekDateHelper.GetSafeFileBaseName(weekStart),
            CreatedAt = DateTime.Now,
            MorningTime = settings.MorningTime.Clone(),
            MidTime = settings.MidTime.Clone(),
            EveningTime = settings.EveningTime.Clone(),
            FullTime = settings.FullTime.Clone(),
            Rows = rows,
            AppliedRules = BuildActiveRulesList(people, settings, priorWeeks.Count, weekStart),
            Warnings = warnings,
            ShowHoursInTable = settings.ShowHoursInTable,
            ShowSummaryColumns = settings.ShowSummaryColumns,
            HasFullShifts = hasAnyFull,
            ComparedHistoryWeeksCount = priorWeeks.Count,
            SimilarityOverlapCount = historyMatches,
            Language = settings.Language,
            MorningColorKey = settings.MorningColorKey,
            MidColorKey = settings.MidColorKey,
            EveningColorKey = settings.EveningColorKey,
            FullColorKey = settings.FullColorKey,
            OffColorKey = settings.OffColorKey
        };

        return new ShiftGenerationResult
        {
            Success = true,
            Schedule = schedule
        };
    }

    private static int?[] AssignLeaveDays(
        IReadOnlyList<PersonConfig> people,
        CustomizationSettings settings,
        int[] candidateScopeDays,
        HashSet<int>[] fullDaysForPerson,
        WeeklySchedule? prev1,
        WeeklySchedule? prev2,
        Random rng)
    {
        int n = people.Count;
        var leaveDays = new int?[n];

        if (settings.NoLeaveForAll)
            return leaveDays;

        if (settings.BulkLastDayLeave)
        {
            for (int p = 0; p < n; p++) leaveDays[p] = 6;
            return leaveDays;
        }

        var dayCounts = new int[7];

        for (int p = 0; p < n; p++)
        {
            if (people[p].LeaveMode == LeaveMode.FixedDay)
            {
                int d = Math.Clamp(people[p].SelectedDayIndex, 0, 6);
                leaveDays[p] = d;
                dayCounts[d]++;
            }
        }

        var randomIndices = Enumerable.Range(0, n)
            .Where(p => people[p].LeaveMode == LeaveMode.Random)
            .OrderBy(_ => rng.Next())
            .ToList();

        // Check who worked Morning on the last day (index 6) of the previous schedule.
        // Only applies to random leave selection; fixed leave days remain unchanged.
        bool hasPrevMorningOnLastDay = prev1 != null && randomIndices.Any(p =>
        {
            string pName = (people[p].Name ?? string.Empty).Trim();
            return FindDayShift(prev1, pName, 6) == ShiftType.Morning;
        });

        if (hasPrevMorningOnLastDay)
        {
            randomIndices = randomIndices
                .OrderByDescending(p =>
                {
                    string pName = (people[p].Name ?? string.Empty).Trim();
                    return FindDayShift(prev1, pName, 6) == ShiftType.Morning ? 1 : 0;
                })
                .ThenBy(_ => rng.Next())
                .ToList();
        }

        foreach (int p in randomIndices)
        {
            string name = (people[p].Name ?? string.Empty).Trim();
            int? prev1Leave = FindLeaveDay(prev1, name);
            int? prev2Leave = FindLeaveDay(prev2, name);
            bool wasMorningLastDay = prev1 != null && FindDayShift(prev1, name, 6) == ShiftType.Morning;

            var validDays = candidateScopeDays.Where(d => !fullDaysForPerson[p].Contains(d)).ToArray();
            if (validDays.Length == 0)
            {
                validDays = Enumerable.Range(0, 7).Where(d => !fullDaysForPerson[p].Contains(d)).ToArray();
            }

            if (validDays.Length == 0)
            {
                leaveDays[p] = null;
                continue;
            }

            var candidates = validDays
                .OrderBy(_ => rng.Next())
                .Select(d =>
                {
                    int penalty = dayCounts[d] * 1000;
                    if (settings.EnableHistoryComparison)
                    {
                        if (prev1Leave == d) penalty += 50;
                        if (prev2Leave == d) penalty += 20;
                    }

                    if (d == 0 && prev1 != null)
                    {
                        if (wasMorningLastDay)
                            penalty -= 300;
                        else if (hasPrevMorningOnLastDay)
                            penalty += 300;
                    }

                    return (day: d, penalty);
                })
                .OrderBy(x => x.penalty)
                .ToList();

            leaveDays[p] = candidates[0].day;
            dayCounts[candidates[0].day]++;
        }

        return leaveDays;
    }

    public static void ComputeDailyQuotas(
        int workers,
        CustomizationSettings settings,
        out int morning,
        out int mid,
        out int evening)
    {
        if (workers <= 0)
        {
            morning = mid = evening = 0;
            return;
        }

        if (workers == 1)
        {
            morning = 1;
            mid = 0;
            evening = 0;
            return;
        }

        if (workers == 2)
        {
            if (settings.TwoWorkerPreferEvening)
            {
                morning = 1;
                mid = 0;
                evening = 1;
            }
            else
            {
                morning = 1;
                mid = 1;
                evening = 0;
            }
            return;
        }

        int targetMorning = settings.EnableDistributionCustomization ? settings.DailyMorningCount : 1;
        int targetMid = settings.EnableDistributionCustomization ? settings.DailyMidCount : 1;

        morning = Math.Clamp(targetMorning, 1, workers);
        int remaining = workers - morning;
        mid = Math.Clamp(targetMid, 0, remaining);
        evening = workers - morning - mid;
    }

    private static ShiftType[,] BuildInitialGrid(
        int n,
        int?[] leaveDays,
        int?[] fullPersonByDay,
        int[] reqMorning,
        int[] reqMid,
        int[] reqEvening,
        Random rng)
    {
        var grid = new ShiftType[n, 7];

        for (int d = 0; d < 7; d++)
        {
            var workingPeople = new List<int>();
            for (int p = 0; p < n; p++)
            {
                if (leaveDays[p] == d)
                    grid[p, d] = ShiftType.Off;
                else if (fullPersonByDay[d] == p)
                    grid[p, d] = ShiftType.Full;
                else
                    workingPeople.Add(p);
            }

            var shuffled = workingPeople.OrderBy(_ => rng.Next()).ToList();
            int idx = 0;
            for (int i = 0; i < reqMorning[d] && idx < shuffled.Count; i++, idx++)
                grid[shuffled[idx], d] = ShiftType.Morning;
            for (int i = 0; i < reqMid[d] && idx < shuffled.Count; i++, idx++)
                grid[shuffled[idx], d] = ShiftType.Mid;
            for (; idx < shuffled.Count; idx++)
                grid[shuffled[idx], d] = ShiftType.Evening;
        }

        return grid;
    }

    private static void OptimizeSwaps(
        ShiftType[,] grid,
        int n,
        List<string> names,
        int?[] leaveDays,
        CustomizationSettings settings,
        WeeklySchedule? prev1,
        WeeklySchedule? prev2,
        Random rng)
    {
        long currentScore = EvaluateScore(grid, n, names, leaveDays, settings, prev1, prev2, out _, out _, out _);

        bool improved = true;
        int pass = 0;
        while (improved && pass < 40)
        {
            improved = false;
            pass++;

            var daysOrder = Enumerable.Range(0, 7).OrderBy(_ => rng.Next()).ToList();
            foreach (int d in daysOrder)
            {
                for (int p1 = 0; p1 < n; p1++)
                {
                    if (grid[p1, d] == ShiftType.Off || grid[p1, d] == ShiftType.Full) continue;

                    for (int p2 = p1 + 1; p2 < n; p2++)
                    {
                        if (grid[p2, d] == ShiftType.Off || grid[p2, d] == ShiftType.Full) continue;
                        if (grid[p1, d] == grid[p2, d]) continue;

                        (grid[p1, d], grid[p2, d]) = (grid[p2, d], grid[p1, d]);
                        long nextScore = EvaluateScore(grid, n, names, leaveDays, settings, prev1, prev2, out _, out _, out _);

                        if (nextScore < currentScore)
                        {
                            currentScore = nextScore;
                            improved = true;
                        }
                        else
                        {
                            (grid[p1, d], grid[p2, d]) = (grid[p2, d], grid[p1, d]);
                        }
                    }
                }
            }
        }
    }

    private static long EvaluateScore(
        ShiftType[,] grid,
        int n,
        List<string> names,
        int?[] leaveDays,
        CustomizationSettings settings,
        WeeklySchedule? prev1,
        WeeklySchedule? prev2,
        out int rule2Violations,
        out int rule4Diff,
        out int historyMatches)
    {
        long score = 0;
        rule2Violations = 0;
        historyMatches = 0;

        var morningCounts = new int[n];
        var midCounts = new int[n];
        var earlyCounts = new int[n];

        for (int p = 0; p < n; p++)
        {
            for (int d = 0; d < 7; d++)
            {
                if (grid[p, d] == ShiftType.Morning) { morningCounts[p]++; earlyCounts[p]++; }
                else if (grid[p, d] == ShiftType.Mid) { midCounts[p]++; earlyCounts[p]++; }
            }
        }

        int minEarly = earlyCounts.Min();
        int maxEarly = earlyCounts.Max();
        rule4Diff = maxEarly - minEarly;

        if (settings.EnableFairEarlyDistribution)
        {
            if (rule4Diff > 1)
                score += (long)(rule4Diff - 1) * 1_000_000L;

            int totalEarly = earlyCounts.Sum();
            double avg = totalEarly / (double)n;
            foreach (int c in earlyCounts)
            {
                double diff = c - avg;
                score += (long)(diff * diff * 20_000);
            }

            int morningDiff = morningCounts.Max() - morningCounts.Min();
            if (morningDiff > 1)
                score += (morningDiff - 1) * 800L;
        }

        if (settings.EnableRuleBeforeAfterLeave && !settings.NoLeaveForAll)
        {
            for (int p = 0; p < n; p++)
            {
                if (!leaveDays[p].HasValue) continue;
                int leaveDay = leaveDays[p]!.Value;
                bool isBulkDay = settings.BulkLastDayLeave && leaveDay == 6;

                if (leaveDay > 0 && !isBulkDay && grid[p, leaveDay - 1] != ShiftType.Off && grid[p, leaveDay - 1] != ShiftType.Full)
                {
                    if (grid[p, leaveDay - 1] != ShiftType.Morning)
                    {
                        rule2Violations++;
                        score += 100_000L;
                    }
                }

                if (leaveDay < 6 && !isBulkDay && grid[p, leaveDay + 1] != ShiftType.Off && grid[p, leaveDay + 1] != ShiftType.Full)
                {
                    if (grid[p, leaveDay + 1] != ShiftType.Evening)
                    {
                        rule2Violations++;
                        score += 100_000L;
                    }
                }
            }
        }

        for (int p = 0; p < n; p++)
        {
            var p1Row = FindPersonRow(prev1, names[p]);
            var p2Row = FindPersonRow(prev2, names[p]);

            for (int d = 0; d < 7; d++)
            {
                if (p1Row != null && p1Row.Days[d] == grid[p, d])
                {
                    historyMatches++;
                    if (settings.EnableHistoryComparison)
                        score += 350L;
                }
                if (p2Row != null && p2Row.Days[d] == grid[p, d])
                {
                    historyMatches++;
                    if (settings.EnableHistoryComparison)
                        score += 140L;
                }
            }

            int streak = 0;
            for (int d = 0; d < 7; d++)
            {
                if (grid[p, d] == ShiftType.Morning)
                {
                    streak++;
                    if (streak >= 3) score += 25L;
                }
                else
                {
                    streak = 0;
                }
            }
        }

        return score;
    }

    private static PersonWeekSchedule? FindPersonRow(WeeklySchedule? schedule, string name)
    {
        if (schedule?.Rows == null) return null;
        return schedule.Rows.FirstOrDefault(r =>
            string.Equals(r.PersonName?.Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    private static ShiftType? FindDayShift(WeeklySchedule? schedule, string name, int dayIndex)
    {
        var row = FindPersonRow(schedule, name);
        if (row?.Days == null || dayIndex < 0 || dayIndex >= row.Days.Length) return null;
        return row.Days[dayIndex];
    }

    private static int? FindLeaveDay(WeeklySchedule? schedule, string name)
    {
        var row = FindPersonRow(schedule, name);
        if (row?.Days == null) return null;
        for (int d = 0; d < 7; d++)
        {
            if (row.Days[d] == ShiftType.Off) return d;
        }
        return null;
    }

    public static List<string> BuildActiveRulesList(
        IReadOnlyList<PersonConfig> people,
        CustomizationSettings settings,
        int previousWeeksFoundCount,
        DateTime? weekStartDate = null)
    {
        var lang = settings.Language;
        var start = (weekStartDate ?? DateTime.Today).Date;
        var rules = new List<string>();
        int count = people.Count;

        string startName = WeekDateHelper.GetDayName(start, 0, lang);
        string endName = WeekDateHelper.GetDayName(start, 6, lang);
        rules.Add(LocalizationService.T(
            lang,
            $"Personel Kapsamı: {count} kişi için {startName} - {endName} haftalık plan hazırlanacak (Min 3, Max 30 kişi).",
            $"Staff Scope: {startName} - {endName} weekly plan for {count} staff (Min 3, Max 30)."));

        if (settings.NoLeaveForAll)
        {
            rules.Add(LocalizationService.T(
                lang,
                "İzin Kuralı: Tüm personel için izin kapatıldı.",
                "Leave Rule: Leave is disabled for all staff this week."));
        }
        else if (settings.BulkLastDayLeave)
        {
            rules.Add(LocalizationService.T(
                lang,
                $"İzin Kuralı (Toplu Seçim): Tüm ekip {endName} günü izinli olacak.",
                $"Leave Rule (Bulk): All staff are off on {endName}."));
        }
        else
        {
            int manualCount = people.Count(p => p.LeaveMode == LeaveMode.FixedDay);
            int randomCount = people.Count(p => p.LeaveMode == LeaveMode.Random);
            int noLeaveCount = people.Count(p => p.LeaveMode == LeaveMode.NoLeave);
            string d0 = WeekDateHelper.GetDayName(start, 0, lang);
            string d4 = WeekDateHelper.GetDayName(start, 4, lang);
            string d5 = WeekDateHelper.GetDayName(start, 5, lang);
            string d6 = WeekDateHelper.GetDayName(start, 6, lang);
            string scopeText = settings.RandomLeaveScope switch
            {
                RandomLeaveScope.FirstFiveDays => $"{d0} - {d4}",
                RandomLeaveScope.FirstSixDays => $"{d0} - {d5}",
                _ => $"{d0} - {d6}"
            };
            string overlapText = settings.AllowLeaveOverlap
                ? LocalizationService.T(lang, "Çakışmaya izin verildi", "Overlap allowed")
                : LocalizationService.T(lang, "İzinler çakışmayacak", "No overlap");
            rules.Add(LocalizationService.T(
                lang,
                $"İzin Kuralı: {manualCount} sabit gün, {randomCount} rastgele ({scopeText}), {noLeaveCount} izin yok • {overlapText}.",
                $"Leave Rule: {manualCount} fixed, {randomCount} random ({scopeText}), {noLeaveCount} none • {overlapText}."));
        }

        if (settings.EnableFullShifts)
        {
            var fullIds = CustomizationSettings.NormalizeFullList(settings.FullPersonIdByDay);
            var fullSummary = new List<string>();
            for (int d = 0; d < 7; d++)
            {
                if (string.IsNullOrWhiteSpace(fullIds[d])) continue;
                var person = people.FirstOrDefault(p => p.Id == fullIds[d]);
                if (person != null)
                    fullSummary.Add($"{WeekDateHelper.GetDayName(start, d, lang)}: {person.Name}");
            }

            rules.Add(fullSummary.Count > 0
                ? LocalizationService.T(lang, $"Full Kalma ({settings.FullTime.Formatted}): {string.Join(" • ", fullSummary)}.", $"Full Shift ({settings.FullTime.Formatted}): {string.Join(" • ", fullSummary)}.")
                : LocalizationService.T(lang, $"Full Kalma ({settings.FullTime.Formatted}): Aktif, henüz gün seçilmedi.", $"Full Shift ({settings.FullTime.Formatted}): Enabled, no day assigned yet."));
        }
        else
        {
            rules.Add(LocalizationService.T(lang, "Full Kalma: Kapalı.", "Full Shift: Off."));
        }

        if (settings.NoLeaveForAll)
        {
            rules.Add(LocalizationService.T(lang, "İzin Öncesi/Sonrası: İzin kapalı olduğu için devre dışı.", "Before/After Leave: Disabled because leave is off."));
        }
        else if (settings.BulkLastDayLeave)
        {
            rules.Add(LocalizationService.T(lang, $"İzin Öncesi/Sonrası: Toplu {endName} izni nedeniyle esnetildi.", $"Before/After Leave: Relaxed due to bulk {endName} leave."));
        }
        else if (settings.EnableRuleBeforeAfterLeave)
        {
            rules.Add(LocalizationService.T(
                lang,
                "İzin Öncesi/Sonrası: İzin gününden bir önceki gün Sabahçı, bir sonraki gün Akşamcı yazılır. İlk gün rastgele izinde önceki shiftin son günündeki Sabahçı kişi önceliklendirilir (sabit izin günleri değişmez).",
                "Before/After Leave: Morning before leave, Evening after leave. For Day-1 random leave, the person who worked Morning on the previous shift's last day is prioritized (fixed leave days stay unchanged)."));
        }
        else
        {
            rules.Add(LocalizationService.T(lang, "İzin Öncesi/Sonrası: Kapalı.", "Before/After Leave: Off."));
        }

        int mCount = settings.EnableDistributionCustomization ? settings.DailyMorningCount : 1;
        int midCount = settings.EnableDistributionCustomization ? settings.DailyMidCount : 1;
        string twoWorkerText = settings.TwoWorkerPreferEvening
            ? LocalizationService.T(lang, "2 kişi kaldığında 1 Sabahçı + 1 Akşamcı", "With 2 workers: 1 Morning + 1 Evening")
            : LocalizationService.T(lang, "2 kişi kaldığında 1 Sabahçı + 1 Ara Shift", "With 2 workers: 1 Morning + 1 Mid Shift");
        rules.Add(LocalizationService.T(
            lang,
            $"Günlük Dağılım & Saatler: {mCount} Sabahçı ({settings.MorningTime.Formatted}), {midCount} Ara Shift ({settings.MidTime.Formatted}), kalanlar Akşamcı ({settings.EveningTime.Formatted}). ({twoWorkerText}).",
            $"Daily Distribution & Hours: {mCount} Morning ({settings.MorningTime.Formatted}), {midCount} Mid ({settings.MidTime.Formatted}), rest Evening ({settings.EveningTime.Formatted}). ({twoWorkerText})."));

        if (settings.EnableFairEarlyDistribution)
        {
            rules.Add(LocalizationService.T(
                lang,
                "Adaletli Dağılım: Sabahçı + Ara Shift aynı kategoride toplanarak eşit (±1 farkla) dağıtılır.",
                "Fair Distribution: Morning + Mid shifts are balanced equally (±1 max difference)."));
        }
        else
        {
            rules.Add(LocalizationService.T(lang, "Adaletli Dağılım: Kapalı.", "Fair Distribution: Off."));
        }

        if (settings.EnableHistoryComparison)
        {
            string flexText = settings.AllowHistoryRelaxation
                ? LocalizationService.T(lang, "sıkıştığında esnetilebilir", "relaxable when tight")
                : LocalizationService.T(lang, "katı uygulanır", "strict");
            rules.Add(previousWeeksFoundCount > 0
                ? LocalizationService.T(lang, $"Benzerlik Önleme: Önceki {previousWeeksFoundCount} hafta ile karşılaştırılır ({flexText}).", $"Similarity Check: Compared against last {previousWeeksFoundCount} week(s) ({flexText}).")
                : LocalizationService.T(lang, $"Benzerlik Önleme: Önceki 2 hafta kontrolü aktif ({flexText}).", $"Similarity Check: Last 2 weeks check active ({flexText})."));
        }
        else
        {
            rules.Add(LocalizationService.T(lang, "Benzerlik Önleme: Kapalı.", "Similarity Check: Off."));
        }

        string fmt = settings.ExportFormat == ImageExportFormat.Jpg ? "JPG" : "PNG";
        rules.Add(LocalizationService.T(
            lang,
            $"Kayıt & Paylaşım: Geçmişe kaydedilir ve '{fmt}' görseli seçili klasöre aktarılır.",
            $"Save & Share: Saved to history and exported as '{fmt}' to the selected folder."));

        return rules;
    }
}
