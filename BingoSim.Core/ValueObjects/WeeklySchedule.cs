namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Represents a weekly play schedule template (not tied to calendar dates).
/// </summary>
public sealed class WeeklySchedule
{
    private readonly List<ScheduledSession> _sessions = [];

    public IReadOnlyList<ScheduledSession> Sessions => _sessions.AsReadOnly();

    public WeeklySchedule() { }

    public WeeklySchedule(IEnumerable<ScheduledSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        _sessions = [.. sessions];
    }

    public void AddSession(ScheduledSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions.Add(session);
    }

    public void RemoveSession(ScheduledSession session)
    {
        _sessions.Remove(session);
    }

    public void ClearSessions()
    {
        _sessions.Clear();
    }

    public void SetSessions(IEnumerable<ScheduledSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        _sessions.Clear();
        _sessions.AddRange(sessions);
    }

    /// <summary>
    /// Gets sessions for a specific day of the week.
    /// </summary>
    public IEnumerable<ScheduledSession> GetSessionsForDay(DayOfWeek day)
    {
        return _sessions.Where(s => s.DayOfWeek == day);
    }

    /// <summary>
    /// Total hours of play time scheduled per week.
    /// </summary>
    public double TotalWeeklyHours => _sessions.Sum(s => s.DurationMinutes) / 60.0;
}
