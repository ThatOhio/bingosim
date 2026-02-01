namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for a row (ordered set of 4 tiles with points 1,2,3,4).
/// </summary>
public record RowDto(int Index, List<TileDto> Tiles);
