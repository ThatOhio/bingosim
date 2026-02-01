namespace BingoSim.Core.Exceptions;

/// <summary>
/// Thrown when an Event is not found by id.
/// </summary>
public sealed class EventNotFoundException(Guid eventId) : Exception($"Event with id '{eventId}' was not found.")
{
    public Guid EventId { get; } = eventId;
}
