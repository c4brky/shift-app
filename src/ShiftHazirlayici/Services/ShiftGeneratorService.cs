// Hey! Sonunda beni bulabildin, seni tebrik ediyorum. <3
// created by c4brky
using System;
using System.Collections.Generic;
using System.Linq;
using ShiftHazirlayici.Models;

namespace ShiftHazirlayici.Services;

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
        if (people == null || people.Count < 3 || people.Count > 10)
        {
            return new ShiftGenerationResult
            {
                Success = false,
                ErrorMessage = "Personel sayısı en az 3, en fazla 10 kişi olmalıdır."
            };
        }

        var trimmedNames = people.Select(p => (p.Name ?? string.Empty).Trim()).ToList();
        if (trimmedNames.Any(string.IsNullOrWhiteSpace))
        {
            return new ShiftGenerationResult
            {
                Success = false,
                ErrorMessage = "Lütfen shift oluşturulacak tüm kişilerin isimlerini doldurun."
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
                ErrorMessage = $"Aynı isim birden fazla kez eklenmiş: \"{duplicateName.Key}\". Lütfen kişilere ayırt edici isimler verin."
            };
        }

        // Validate manual leave overlaps if not in bulk/no-leave mode and overlap not allowed
        if (!settings.BulkSundayLeave && !settings.NoLeaveForAll && !settings.AllowLeaveOverlap)
        {
            var manualOverlap = people
                .Where(p => p.LeaveMode == LeaveMode.BelirliGun)
                .GroupBy(p => p.SelectedDayIndex)
                .FirstOrDefault(g => g.Count() > 1);

            if (manualOverlap != null)
            {
                string dayName = PersonConfig.DayNamesTr[Math.Clamp(manualOverlap.Key, 0, 6)];
                return new ShiftGenerationResult
                {
                    Success = false,
                    ErrorMessage = $"İzinler çakışamaz: Birden fazla kişiye '{dayName}' günü manuel izin atanmış. Lütfen günleri değiştirin veya Özelleştir bölümünden izin çakışmasına izin verin."
                };
            }
        }

        // Resolve optional Full-day assignments (dayIndex -> personIndex)
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

                // Check conflict with explicit leave on the same day
                if (!settings.NoLeaveForAll)
                {
                    if (settings.BulkSundayLeave && d == 6)
                    {
                        return new ShiftGenerationResult
                        {
                            Success = false,
                            ErrorMessage = $"Çakışma: '{trimmedNames[pIdx]}' Pazar günü hem Toplu İzinli hem Full seçilmiş. Pazar Full seçimini kaldırın veya Toplu Pazar İznini kapatın."
                        };
                    }

                    if (!settings.BulkSundayLeave && people[pIdx].LeaveMode == LeaveMode.BelirliGun && people[pIdx].SelectedDayIndex == d)
                    {
                        return new ShiftGenerationResult
                        {
                            Success = false,
                            ErrorMessage = $"Çakışma: '{trimmedNames[pIdx]}' {PersonConfig.DayNamesTr[d]} günü hem manuel İzinli hem Full seçilmiş."
                        };
                    }
                }

                fullPersonByDay[d] = pIdx;
                fullDaysForPerson[pIdx].Add(d);
                hasAnyFull = true;
            }
        }

        var monday = WeekDateHelper.GetMonday(targetDate);
        var sunday = monday.AddDays(6);

        var priorWeeks = recentSchedules
            .Where(s => s.WeekStartMonday.Date < monday.Date)
            .OrderByDescending(s => s.WeekStartMonday)
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
            RandomLeaveScope.HaftaIciPztCum => new[] { 0, 1, 2, 3, 4 },
            RandomLeaveScope.PztCumartesi => new[] { 0, 1, 2, 3, 4, 5 },
            _ => new[] { 0, 1, 2, 3, 4, 5, 6 }
        };

        var warnings = new List<string>();
        bool hasRandomLeave = !settings.BulkSundayLeave && !settings.NoLeaveForAll &&
                              people.Any(p => p.LeaveMode == LeaveMode.Rastgele);

        int leaveTrials = hasRandomLeave ? 18 : 1;
        int scheduleRestartsPerLeave = 30;

        long bestScore = long.MaxValue;
        ShiftType[,]? bestGrid = null;
        int?[]? bestLeaveDays = null;

        for (int lt = 0; lt < leaveTrials; lt++)
        {
            int?[] leaveDays = AssignLeaveDays(people, settings, candidateScopeDays, fullDaysForPerson, prev1, prev2, rng);

            var reqSabah = new int[7];
            var reqAra = new int[7];
            var reqAksam = new int[7];

            for (int d = 0; d < 7; d++)
            {
                int regularWorkers = 0;
                for (int p = 0; p < n; p++)
                {
                    if (leaveDays[p] == d) continue;
                    if (fullPersonByDay[d] == p) continue;
                    regularWorkers++;
                }

                ComputeDailyQuotas(regularWorkers, settings, out reqSabah[d], out reqAra[d], out reqAksam[d]);
            }

            for (int restart = 0; restart < scheduleRestartsPerLeave; restart++)
            {
                var grid = BuildInitialGrid(n, leaveDays, fullPersonByDay, reqSabah, reqAra, reqAksam, rng);
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
                ErrorMessage = "Shift planı oluşturulurken beklenmeyen bir hata oluştu."
            };
        }

        EvaluateScore(bestGrid, n, trimmedNames, bestLeaveDays, settings, prev1, prev2,
            out int rule2Violations, out int rule4Diff, out int historyMatches);

        if (rule2Violations > 0 && settings.EnableRuleBeforeAfterLeave)
        {
            warnings.Add($"İzin çakışması veya düşük çalışma sayısı nedeniyle 'izin öncesi sabah / sonrası akşam' kuralı {rule2Violations} noktada esnetildi.");
        }

        if (rule4Diff > 1 && settings.EnableFairEarlyDistribution)
        {
            warnings.Add("Özel koşullar nedeniyle Sabah+Ara shift dağılımında 1'den fazla fark oluştu.");
        }

        if (!settings.BulkSundayLeave && !settings.NoLeaveForAll)
        {
            var overlapDays = Enumerable.Range(0, 7)
                .Where(d => bestLeaveDays.Count(ld => ld == d) > 1)
                .ToList();
            if (overlapDays.Count > 0)
            {
                string dayList = string.Join(", ", overlapDays.Select(d => PersonConfig.DayNamesTr[d]));
                warnings.Add($"Kişi sayısı ve seçilen izin kapsamı nedeniyle şu gün(ler)de birden fazla kişi izinli oldu: {dayList}.");
            }
        }

        if (settings.EnableHistoryComparison && priorWeeks.Count > 0 && historyMatches > 0 && settings.AllowHistoryRelaxation)
        {
            int totalCells = n * 7;
            if (historyMatches > totalCells / 3)
            {
                warnings.Add($"Önceki {priorWeeks.Count} haftayla tam farklılık kısıtlar nedeniyle kısmen esnetildi ({historyMatches} hücre eşleşti).");
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
                    case ShiftType.Sabah: row.SabahCount++; break;
                    case ShiftType.Ara: row.AraCount++; break;
                    case ShiftType.Aksam: row.AksamCount++; break;
                    case ShiftType.Izin: row.IzinCount++; break;
                    case ShiftType.Full: row.FullCount++; break;
                }
            }

            rows.Add(row);
        }

        var schedule = new WeeklySchedule
        {
            Id = Guid.NewGuid().ToString("N"),
            WeekStartMonday = monday,
            WeekEndSunday = sunday,
            WeekDisplayLabel = WeekDateHelper.GetWeekDisplayLabel(monday),
            FileBaseName = WeekDateHelper.GetSafeFileBaseName(monday),
            CreatedAt = DateTime.Now,
            SabahTime = settings.SabahTime.Clone(),
            AraTime = settings.AraTime.Clone(),
            AksamTime = settings.AksamTime.Clone(),
            FullTime = settings.FullTime.Clone(),
            Rows = rows,
            AppliedRules = BuildActiveRulesList(people, settings, priorWeeks.Count),
            Warnings = warnings,
            ShowHoursInTable = settings.ShowHoursInTable,
            ShowSummaryColumns = settings.ShowSummaryColumns,
            HasFullShifts = hasAnyFull,
            ComparedHistoryWeeksCount = priorWeeks.Count,
            SimilarityOverlapCount = historyMatches
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

        if (settings.BulkSundayLeave)
        {
            for (int p = 0; p < n; p++) leaveDays[p] = 6;
            return leaveDays;
        }

        var dayCounts = new int[7];

        for (int p = 0; p < n; p++)
        {
            if (people[p].LeaveMode == LeaveMode.BelirliGun)
            {
                int d = Math.Clamp(people[p].SelectedDayIndex, 0, 6);
                leaveDays[p] = d;
                dayCounts[d]++;
            }
        }

        var randomIndices = Enumerable.Range(0, n)
            .Where(p => people[p].LeaveMode == LeaveMode.Rastgele)
            .OrderBy(_ => rng.Next())
            .ToList();

        foreach (int p in randomIndices)
        {
            string name = (people[p].Name ?? string.Empty).Trim();
            int? prev1Leave = FindLeaveDay(prev1, name);
            int? prev2Leave = FindLeaveDay(prev2, name);

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
        out int sabah,
        out int ara,
        out int aksam)
    {
        if (workers <= 0)
        {
            sabah = ara = aksam = 0;
            return;
        }

        if (workers == 1)
        {
            sabah = 1;
            ara = 0;
            aksam = 0;
            return;
        }

        if (workers == 2)
        {
            if (settings.TwoWorkerPreferAksam)
            {
                sabah = 1;
                ara = 0;
                aksam = 1;
            }
            else
            {
                sabah = 1;
                ara = 1;
                aksam = 0;
            }
            return;
        }

        int targetSabah = settings.EnableDistributionCustomization ? settings.DailySabahCount : 1;
        int targetAra = settings.EnableDistributionCustomization ? settings.DailyAraCount : 1;

        sabah = Math.Clamp(targetSabah, 1, workers);
        int remaining = workers - sabah;
        ara = Math.Clamp(targetAra, 0, remaining);
        aksam = workers - sabah - ara;
    }

    private static ShiftType[,] BuildInitialGrid(
        int n,
        int?[] leaveDays,
        int?[] fullPersonByDay,
        int[] reqSabah,
        int[] reqAra,
        int[] reqAksam,
        Random rng)
    {
        var grid = new ShiftType[n, 7];

        for (int d = 0; d < 7; d++)
        {
            var workingPeople = new List<int>();
            for (int p = 0; p < n; p++)
            {
                if (leaveDays[p] == d)
                {
                    grid[p, d] = ShiftType.Izin;
                }
                else if (fullPersonByDay[d] == p)
                {
                    grid[p, d] = ShiftType.Full;
                }
                else
                {
                    workingPeople.Add(p);
                }
            }

            var shuffled = workingPeople.OrderBy(_ => rng.Next()).ToList();
            int idx = 0;
            for (int i = 0; i < reqSabah[d] && idx < shuffled.Count; i++, idx++)
                grid[shuffled[idx], d] = ShiftType.Sabah;
            for (int i = 0; i < reqAra[d] && idx < shuffled.Count; i++, idx++)
                grid[shuffled[idx], d] = ShiftType.Ara;
            for (; idx < shuffled.Count; idx++)
                grid[shuffled[idx], d] = ShiftType.Aksam;
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
                    if (grid[p1, d] == ShiftType.Izin || grid[p1, d] == ShiftType.Full) continue;

                    for (int p2 = p1 + 1; p2 < n; p2++)
                    {
                        if (grid[p2, d] == ShiftType.Izin || grid[p2, d] == ShiftType.Full) continue;
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

        var sabahCounts = new int[n];
        var araCounts = new int[n];
        var earlyCounts = new int[n];

        for (int p = 0; p < n; p++)
        {
            for (int d = 0; d < 7; d++)
            {
                if (grid[p, d] == ShiftType.Sabah) { sabahCounts[p]++; earlyCounts[p]++; }
                else if (grid[p, d] == ShiftType.Ara) { araCounts[p]++; earlyCounts[p]++; }
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

            int sabahDiff = sabahCounts.Max() - sabahCounts.Min();
            if (sabahDiff > 1)
                score += (sabahDiff - 1) * 800L;
        }

        if (settings.EnableRuleBeforeAfterLeave && !settings.NoLeaveForAll)
        {
            for (int p = 0; p < n; p++)
            {
                if (!leaveDays[p].HasValue) continue;
                int L = leaveDays[p]!.Value;
                bool isBulkDay = settings.BulkSundayLeave && L == 6;

                if (L > 0 && !isBulkDay && grid[p, L - 1] != ShiftType.Izin && grid[p, L - 1] != ShiftType.Full)
                {
                    if (grid[p, L - 1] != ShiftType.Sabah)
                    {
                        rule2Violations++;
                        score += 100_000L;
                    }
                }

                if (L < 6 && !isBulkDay && grid[p, L + 1] != ShiftType.Izin && grid[p, L + 1] != ShiftType.Full)
                {
                    if (grid[p, L + 1] != ShiftType.Aksam)
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
                if (grid[p, d] == ShiftType.Sabah)
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

    private static int? FindLeaveDay(WeeklySchedule? schedule, string name)
    {
        var row = FindPersonRow(schedule, name);
        if (row?.Days == null) return null;
        for (int d = 0; d < 7; d++)
        {
            if (row.Days[d] == ShiftType.Izin) return d;
        }
        return null;
    }

    public static List<string> BuildActiveRulesList(
        IReadOnlyList<PersonConfig> people,
        CustomizationSettings settings,
        int previousWeeksFoundCount)
    {
        var rules = new List<string>();
        int count = people.Count;

        rules.Add($"Personel Kapsamı: Toplam {count} kişi için Pazartesi - Pazar haftalık plan hazırlanacak (Min 3, Max 10 kişi).");

        if (settings.NoLeaveForAll)
        {
            rules.Add("İzin Kuralı: Tüm personel için izin kapatıldı (Bu hafta kimseye izin yazılmayacak).");
        }
        else if (settings.BulkSundayLeave)
        {
            rules.Add("İzin Kuralı (Toplu Seçim): Toplu Pazar İzni aktif — tüm ekip Pazar günü izinli olacak, Pazartesi-Cumartesi normal çalışacak.");
        }
        else
        {
            int manualCount = people.Count(p => p.LeaveMode == LeaveMode.BelirliGun);
            int randomCount = people.Count(p => p.LeaveMode == LeaveMode.Rastgele);
            int noLeaveCount = people.Count(p => p.LeaveMode == LeaveMode.IzinYok);
            string scopeText = settings.RandomLeaveScope switch
            {
                RandomLeaveScope.HaftaIciPztCum => "Hafta içi (Pzt-Cum)",
                RandomLeaveScope.PztCumartesi => "Pzt-Cmt",
                _ => "Tüm hafta (Pzt-Paz)"
            };
            string overlapText = settings.AllowLeaveOverlap
                ? "Çakışmaya izin verildi"
                : "İzinler çakışmayacak (kapasite izin verdiği sürece)";
            rules.Add($"İzin Kuralı: {manualCount} sabit gün, {randomCount} rastgele ({scopeText}), {noLeaveCount} izin yok • {overlapText}.");
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
                    fullSummary.Add($"{PersonConfig.DayNamesTr[d]}: {person.Name}");
            }

            rules.Add(fullSummary.Count > 0
                ? $"Full Kalma ({settings.FullTime.Formatted}): {string.Join(" • ", fullSummary)}."
                : $"Full Kalma ({settings.FullTime.Formatted}): Seçenek açık ancak henüz gün ataması yapılmadı.");
        }
        else
        {
            rules.Add("Full Kalma: Kapalı (İsteğe bağlı — özelleştirmeden gün gün kişi seçilebilir).");
        }

        if (settings.NoLeaveForAll)
        {
            rules.Add("İzin Öncesi/Sonrası: İzin kapalı olduğu için bu kural devre dışı.");
        }
        else if (settings.BulkSundayLeave)
        {
            rules.Add("İzin Öncesi/Sonrası: Toplu Pazar izni nedeniyle Cumartesi herkesin sabahçı olması fiziksel olarak esnetildi.");
        }
        else if (settings.EnableRuleBeforeAfterLeave)
        {
            rules.Add("İzin Öncesi/Sonrası: Her çalışanın izin gününden bir önceki gün Sabahçı, bir sonraki gün Akşamcı yazılacak.");
        }
        else
        {
            rules.Add("İzin Öncesi/Sonrası: Özelleştirmeden kapatıldı.");
        }

        int sCount = settings.EnableDistributionCustomization ? settings.DailySabahCount : 1;
        int aCount = settings.EnableDistributionCustomization ? settings.DailyAraCount : 1;
        string twoWorkerText = settings.TwoWorkerPreferAksam
            ? "2 kişi kaldığında 1 Sabahçı + 1 Akşamcı"
            : "2 kişi kaldığında 1 Sabahçı + 1 Ara Shift";
        rules.Add($"Günlük Dağılım & Saatler: Her gün {sCount} Sabahçı ({settings.SabahTime.Formatted}), {aCount} Ara Shift ({settings.AraTime.Formatted}), kalanlar Akşamcı ({settings.AksamTime.Formatted}). ({twoWorkerText}).");

        if (settings.EnableFairEarlyDistribution)
        {
            rules.Add("Adaletli Dağılım: Sabahçı + Ara Shift aynı kategoride toplanarak kişiler arasında eşit (en fazla ±1 farkla) dağıtılacak.");
        }
        else
        {
            rules.Add("Adaletli Dağılım: Özelleştirmeden kapatıldı.");
        }

        if (settings.EnableHistoryComparison)
        {
            string flexText = settings.AllowHistoryRelaxation ? "çözüm sıkıştığında esnetilebilir" : "katı uygulanır";
            rules.Add(previousWeeksFoundCount > 0
                ? $"Benzerlik Önleme: Önceki {previousWeeksFoundCount} kayıtlı hafta ile karşılaştırılıp farklılık sağlanacak ({flexText})."
                : $"Benzerlik Önleme: Önceki 2 hafta kontrolü aktif (henüz geçmiş kayıt yok, rastgele üretim uygulanacak; {flexText}).");
        }
        else
        {
            rules.Add("Benzerlik Önleme: Geçmiş hafta kontrolü kapatıldı.");
        }

        string fmt = settings.ExportFormat == ImageExportFormat.Jpg ? "JPG" : "PNG";
        rules.Add($"Kayıt & Paylaşım: Oluşturulduğunda yerel geçmişe kaydedilecek ve '{fmt}' görseli seçili klasöre aktarılacak.");

        return rules;
    }
}
