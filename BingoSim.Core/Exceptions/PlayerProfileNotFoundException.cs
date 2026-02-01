namespace BingoSim.Core.Exceptions;

/// <summary>
/// Exception thrown when a PlayerProfile is not found.
/// </summary>
public class PlayerProfileNotFoundException : Exception
{
    public Guid PlayerProfileId { get; }

    public PlayerProfileNotFoundException(Guid playerProfileId)
        : base($"PlayerProfile with ID {playerProfileId} was not found.")
    {
        PlayerProfileId = playerProfileId;
    }

    public PlayerProfileNotFoundException(Guid playerProfileId, Exception innerException)
        : base($"PlayerProfile with ID {playerProfileId} was not found.", innerException)
    {
        PlayerProfileId = playerProfileId;
    }
}
