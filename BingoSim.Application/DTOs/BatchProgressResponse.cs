namespace BingoSim.Application.DTOs;

public sealed class BatchProgressResponse
{
    public int Completed { get; init; }
    public int Failed { get; init; }
    public int Running { get; init; }
    public int Pending { get; init; }
}
