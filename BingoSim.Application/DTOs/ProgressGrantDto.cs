namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for a progress grant (drop key and units).
/// Supports fixed units or variable range (UnitsMin–UnitsMax) sampled at runtime.
/// </summary>
public record ProgressGrantDto(string DropKey, int Units, int? UnitsMin = null, int? UnitsMax = null);
