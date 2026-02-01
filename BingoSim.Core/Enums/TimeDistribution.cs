namespace BingoSim.Core.Enums;

/// <summary>
/// Distribution used to sample attempt time (e.g. Uniform, NormalApprox, Custom).
/// </summary>
public enum TimeDistribution
{
    Uniform = 0,
    NormalApprox = 1,
    Custom = 2
}
