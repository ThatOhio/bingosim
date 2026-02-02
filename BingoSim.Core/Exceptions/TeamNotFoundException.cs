namespace BingoSim.Core.Exceptions;

/// <summary>
/// Thrown when a requested team is not found.
/// </summary>
public sealed class TeamNotFoundException(Guid teamId) : Exception($"Team with id '{teamId}' was not found.");
