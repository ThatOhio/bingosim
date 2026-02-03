using BingoSim.Application.Simulation;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

/// <summary>
/// Tests for SimulationNoProgressException and the no-progress guard behavior.
/// </summary>
public class SimulationNoProgressGuardTests
{
    [Fact]
    public void SimulationNoProgressException_ContainsDiagnostics()
    {
        var ex = new SimulationNoProgressException(
            "Test message",
            simTime: 1000,
            nextSimTime: 1000,
            simTimeEt: "2025-02-03T10:00:00-05:00",
            nextSimTimeEt: "2025-02-03T10:00:00-05:00",
            onlinePlayersCount: 0);

        ex.Message.Should().Be("Test message");
        ex.SimTime.Should().Be(1000);
        ex.NextSimTime.Should().Be(1000);
        ex.SimTimeEt.Should().Be("2025-02-03T10:00:00-05:00");
        ex.NextSimTimeEt.Should().Be("2025-02-03T10:00:00-05:00");
        ex.OnlinePlayersCount.Should().Be(0);
    }

    [Fact]
    public void SimulationNoProgressException_IsInvalidOperationException()
    {
        var ex = new SimulationNoProgressException(
            "Guard triggered",
            simTime: 0,
            nextSimTime: 0,
            simTimeEt: "2025-02-03T09:00:00-05:00",
            nextSimTimeEt: "2025-02-03T09:00:00-05:00",
            onlinePlayersCount: 8);

        ex.Should().BeAssignableTo<InvalidOperationException>();
    }

    [Fact]
    public void Execute_AlwaysOnlineSnapshot_CompletesWithoutNoProgressException()
    {
        var snapshotJson = PerfScenarioSnapshot.BuildJson();
        var allocatorFactory = new BingoSim.Application.Simulation.Allocation.ProgressAllocatorFactory();
        var runner = new BingoSim.Application.Simulation.Runner.SimulationRunner(allocatorFactory);

        var results = runner.Execute(snapshotJson, "perf-baseline-2025_0", CancellationToken.None);

        results.Should().NotBeEmpty();
    }

    [Fact]
    public void Execute_CancelledToken_ThrowsOperationCanceledException()
    {
        var snapshotJson = PerfScenarioSnapshot.BuildJson();
        var allocatorFactory = new BingoSim.Application.Simulation.Allocation.ProgressAllocatorFactory();
        var runner = new BingoSim.Application.Simulation.Runner.SimulationRunner(allocatorFactory);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => runner.Execute(snapshotJson, "perf-baseline-2025_0", cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }
}
