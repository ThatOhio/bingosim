namespace BingoSim.Core.Exceptions;

/// <summary>
/// Exception thrown when an ActivityDefinition is not found.
/// </summary>
public class ActivityDefinitionNotFoundException : Exception
{
    public Guid ActivityDefinitionId { get; }

    public ActivityDefinitionNotFoundException(Guid activityDefinitionId)
        : base($"ActivityDefinition with ID {activityDefinitionId} was not found.")
    {
        ActivityDefinitionId = activityDefinitionId;
    }

    public ActivityDefinitionNotFoundException(Guid activityDefinitionId, Exception innerException)
        : base($"ActivityDefinition with ID {activityDefinitionId} was not found.", innerException)
    {
        ActivityDefinitionId = activityDefinitionId;
    }
}
