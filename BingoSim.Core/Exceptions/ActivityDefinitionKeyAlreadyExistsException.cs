namespace BingoSim.Core.Exceptions;

/// <summary>
/// Exception thrown when an ActivityDefinition key is already in use.
/// </summary>
public class ActivityDefinitionKeyAlreadyExistsException : Exception
{
    public string Key { get; }

    public ActivityDefinitionKeyAlreadyExistsException(string key)
        : base($"An ActivityDefinition with key '{key}' already exists.")
    {
        Key = key;
    }
}
