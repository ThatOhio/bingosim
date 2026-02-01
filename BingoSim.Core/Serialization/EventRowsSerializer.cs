using System.Text.Json;
using BingoSim.Core.ValueObjects;

namespace BingoSim.Core.Serialization;

/// <summary>
/// Serializes Event.Rows to/from JSON. Used by Event entity and Infrastructure for persistence.
/// </summary>
public static class EventRowsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null, // PascalCase to match C# properties
        WriteIndented = false
    };

    public static string ToJson(List<Row> rows)
    {
        return JsonSerializer.Serialize(rows, Options);
    }

    public static List<Row> FromJson(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return [];

        return JsonSerializer.Deserialize<List<Row>>(json, Options) ?? [];
    }
}
