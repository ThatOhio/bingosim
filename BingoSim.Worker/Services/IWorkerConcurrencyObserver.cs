namespace BingoSim.Worker.Services;

/// <summary>
/// Optional observer for concurrency validation in tests. Not used in production.
/// </summary>
public interface IWorkerConcurrencyObserver
{
    void OnConsumeStarted();
    void OnConsumeEnded();
}
