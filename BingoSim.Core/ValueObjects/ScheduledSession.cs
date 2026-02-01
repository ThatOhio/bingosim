namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Represents a single play session window template.
/// </summary>
public sealed record ScheduledSession
{
    public DayOfWeek DayOfWeek { get; }
    public TimeOnly StartLocalTime { get; }
    public int DurationMinutes { get; }

    public ScheduledSession(DayOfWeek dayOfWeek, TimeOnly startLocalTime, int durationMinutes)
    {
        if (durationMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Duration must be greater than zero.");

        DayOfWeek = dayOfWeek;
        StartLocalTime = startLocalTime;
        DurationMinutes = durationMinutes;
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private ScheduledSession() { }

    public TimeOnly EndLocalTime => StartLocalTime.AddMinutes(DurationMinutes);
}
