using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation.Schedule;

/// <summary>
/// Pure functions for evaluating player availability against weekly schedules.
/// All times are interpreted in America/New_York (ET).
/// </summary>
public static class ScheduleEvaluator
{
    /// <summary>Timezone for schedule interpretation. All timestamps converted to ET.</summary>
    public static readonly TimeZoneInfo EasternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private const int MinutesPerDay = 1440;

    /// <summary>
    /// Precomputed daily window: (day 0-6, start minutes, end minutes). End is exclusive.
    /// For midnight-spanning sessions, EndMinNextDay is set for the main-part interval.
    /// Built once from schedule for fast lookups.
    /// </summary>
    public sealed class DailyWindows
    {
        private readonly List<(int Day, int StartMin, int EndMin, int? EndMinNextDay)> _intervals = [];

        /// <summary>Schedule is required. Sessions null/empty = always online.</summary>
        public static DailyWindows Build(WeeklyScheduleSnapshotDto schedule)
        {
            var w = new DailyWindows();
            if (schedule.Sessions is not { Count: > 0 })
                return w;

            foreach (var s in schedule.Sessions)
            {
                var start = Math.Clamp(s.StartLocalTimeMinutes, 0, MinutesPerDay - 1);
                var end = start + s.DurationMinutes;

                if (end <= MinutesPerDay)
                {
                    w._intervals.Add((s.DayOfWeek, start, end, null));
                }
                else
                {
                    w._intervals.Add((s.DayOfWeek, start, MinutesPerDay, end - MinutesPerDay));
                    w._intervals.Add(((s.DayOfWeek + 1) % 7, 0, end - MinutesPerDay, null));
                }
            }
            return w;
        }

        public bool Contains(int day, int minutesFromMidnight)
        {
            foreach (var (d, start, end, _) in _intervals)
            {
                if (d == day && minutesFromMidnight >= start && minutesFromMidnight < end)
                    return true;
            }
            return false;
        }

        public bool IsAlwaysOnline => _intervals.Count == 0;

        internal IReadOnlyList<(int Day, int StartMin, int EndMin, int? EndMinNextDay)> Intervals => _intervals;
    }

    /// <summary>
    /// Returns true if the player is online at the given ET timestamp.
    /// Schedule is required; Sessions null/empty = always online.
    /// </summary>
    public static bool IsOnlineAt(WeeklyScheduleSnapshotDto schedule, DateTimeOffset timestampEt)
    {
        var windows = DailyWindows.Build(schedule);
        return IsOnlineAt(windows, timestampEt);
    }

    /// <summary>
    /// Returns true if the player is online at the given ET timestamp. Uses precomputed DailyWindows.
    /// </summary>
    public static bool IsOnlineAt(DailyWindows windows, DateTimeOffset timestampEt)
    {
        if (windows.IsAlwaysOnline)
            return true;

        var et = ToEastern(timestampEt);
        return windows.Contains((int)et.DayOfWeek, et.Hour * 60 + et.Minute);
    }

    /// <summary>
    /// Returns the end time of the session containing the given timestamp, or null if offline.
    /// </summary>
    public static DateTimeOffset? GetCurrentSessionEnd(WeeklyScheduleSnapshotDto schedule, DateTimeOffset timestampEt)
    {
        var windows = DailyWindows.Build(schedule);
        return GetCurrentSessionEnd(windows, timestampEt);
    }

    /// <summary>
    /// Returns the end time of the session containing the given timestamp, or null if offline. Uses precomputed DailyWindows.
    /// </summary>
    public static DateTimeOffset? GetCurrentSessionEnd(DailyWindows windows, DateTimeOffset timestampEt)
    {
        if (windows.IsAlwaysOnline)
            return null;

        var et = ToEastern(timestampEt);
        var day = (int)et.DayOfWeek;
        var min = et.Hour * 60 + et.Minute;

        foreach (var (d, start, end, endMinNextDay) in windows.Intervals)
        {
            if (d != day || min < start || min >= end)
                continue;

            var sessionEndDt = endMinNextDay is { } nextDay
                ? et.Date.AddDays(1).AddMinutes(nextDay)
                : et.Date.AddMinutes(end);
            return new DateTimeOffset(sessionEndDt, EasternTimeZone.GetUtcOffset(sessionEndDt));
        }
        return null;
    }

    /// <summary>
    /// Returns the next session start at or after the given ET timestamp, or null if no sessions.
    /// Uses raw sessions (not precomputed) to avoid duplicate starts from midnight-spanning splits.
    /// </summary>
    public static DateTimeOffset? GetNextSessionStart(WeeklyScheduleSnapshotDto schedule, DateTimeOffset fromEt)
    {
        if (schedule.Sessions is not { Count: > 0 })
            return null;

        var et = ToEastern(fromEt);
        var fromMinutes = et.Hour * 60 + et.Minute;

        DateTimeOffset? best = null;
        for (var d = 0; d < 8; d++)
        {
            var checkDate = et.Date.AddDays(d);
            var checkDay = (int)checkDate.DayOfWeek;

            foreach (var s in schedule.Sessions)
            {
                if (s.DayOfWeek != checkDay)
                    continue;
                var startMin = Math.Clamp(s.StartLocalTimeMinutes, 0, MinutesPerDay - 1);
                if (d == 0 && startMin < fromMinutes)
                    continue;
                if (d == 0 && startMin == fromMinutes)
                    continue;

                var candidate = checkDate.AddMinutes(startMin);
                var candidateDto = new DateTimeOffset(candidate, EasternTimeZone.GetUtcOffset(candidate));
                if (candidateDto < fromEt)
                    continue;
                if (best is null || candidateDto < best)
                    best = candidateDto;
            }
        }
        return best;
    }

    /// <summary>
    /// Converts sim time (seconds from event start) to ET timestamp.
    /// </summary>
    public static DateTimeOffset SimTimeToEt(DateTimeOffset eventStartEt, int simTimeSeconds) =>
        eventStartEt.AddSeconds(simTimeSeconds);

    /// <summary>
    /// Converts ET timestamp to sim time (seconds from event start).
    /// </summary>
    public static int EtToSimTime(DateTimeOffset eventStartEt, DateTimeOffset timestampEt) =>
        (int)(timestampEt - eventStartEt).TotalSeconds;

    /// <summary>
    /// Returns the earliest session start across all players at or after currentEt, or null.
    /// </summary>
    public static DateTimeOffset? GetEarliestNextSessionStart(EventSnapshotDto snapshot, DateTimeOffset currentEt)
    {
        DateTimeOffset? earliest = null;
        foreach (var team in snapshot.Teams)
        {
            foreach (var player in team.Players)
            {
                var next = GetNextSessionStart(player.Schedule, currentEt);
                if (next is null)
                    continue;
                if (earliest is null || next < earliest)
                    earliest = next;
            }
        }
        return earliest;
    }

    private static DateTimeOffset ToEastern(DateTimeOffset dto) =>
        TimeZoneInfo.ConvertTime(dto, EasternTimeZone);
}
