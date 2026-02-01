using System.ComponentModel.DataAnnotations.Schema;
using BingoSim.Core.Serialization;
using BingoSim.Core.ValueObjects;

namespace BingoSim.Core.Entities;

/// <summary>
/// Represents one configured community event and its board (ordered rows and tiles).
/// </summary>
public class Event
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TimeSpan Duration { get; private set; }
    public int UnlockPointsRequiredPerRow { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>JSON-serialized rows. EF maps this column; Rows property deserializes on access.</summary>
    private string _rowsJson = "[]";

    /// <summary>Computed from _rowsJson; not mapped by EF.</summary>
    [NotMapped]
    public IReadOnlyList<Row> Rows => EventRowsSerializer.FromJson(_rowsJson);

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private Event() { }

    public Event(string name, TimeSpan duration, int unlockPointsRequiredPerRow = 5)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than zero.");

        if (unlockPointsRequiredPerRow < 0)
            throw new ArgumentOutOfRangeException(nameof(unlockPointsRequiredPerRow), "UnlockPointsRequiredPerRow cannot be negative.");

        Id = Guid.NewGuid();
        Name = name.Trim();
        Duration = duration;
        UnlockPointsRequiredPerRow = unlockPointsRequiredPerRow;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name.Trim();
    }

    public void UpdateDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than zero.");

        Duration = duration;
    }

    public void SetUnlockPointsRequiredPerRow(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "UnlockPointsRequiredPerRow cannot be negative.");

        UnlockPointsRequiredPerRow = value;
    }

    public void SetRows(IEnumerable<Row> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var rowList = rows.OrderBy(r => r.Index).ToList();

        // Validate no duplicate row indices
        var indices = rowList.Select(r => r.Index).ToList();
        if (indices.Distinct().Count() != indices.Count)
            throw new InvalidOperationException("Row indices must be unique within the event.");

        // Validate each row has exactly 4 tiles with points 1,2,3,4
        foreach (var row in rowList)
        {
            if (row.Tiles.Count != 4)
                throw new InvalidOperationException($"Row at index {row.Index} must contain exactly 4 tiles.");

            var points = row.Tiles.Select(t => t.Points).OrderBy(p => p).ToList();
            if (!points.SequenceEqual([1, 2, 3, 4]))
                throw new InvalidOperationException($"Row at index {row.Index} must contain exactly one tile per point value 1, 2, 3, 4.");
        }

        // Validate tile keys unique across the entire event
        var allKeys = rowList.SelectMany(r => r.Tiles.Select(t => t.Key)).ToList();
        if (allKeys.Distinct(StringComparer.Ordinal).Count() != allKeys.Count)
            throw new InvalidOperationException("Tile keys must be unique within the event.");

        _rowsJson = EventRowsSerializer.ToJson(rowList);
    }
}
