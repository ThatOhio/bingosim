namespace BingoSim.Application.Simulation.Snapshot;

public sealed class ProgressGrantSnapshotDto
{
    public required string DropKey { get; init; }
    /// <summary>Fixed units when UnitsMin/UnitsMax are not set.</summary>
    public required int Units { get; init; }
    /// <summary>Minimum units for variable grants. When set with UnitsMax, units are sampled at runtime.</summary>
    public int? UnitsMin { get; init; }
    /// <summary>Maximum units for variable grants. When set with UnitsMin, units are sampled at runtime.</summary>
    public int? UnitsMax { get; init; }

    /// <summary>True when this grant uses variable units.</summary>
    public bool IsVariable => UnitsMin.HasValue && UnitsMax.HasValue;
}
