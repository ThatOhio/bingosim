using BingoSim.Application.DTOs;
using BingoSim.Application.Interfaces;
using BingoSim.Core.Enums;
using BingoSim.Web.Components.Pages.Simulations;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BingoSim.Web.Tests.Pages.Simulations;

public class SimulationResultsTests : BunitContext
{
    private readonly ISimulationBatchService _batchService = Substitute.For<ISimulationBatchService>();
    private readonly Guid _batchId = Guid.NewGuid();

    public SimulationResultsTests()
    {
        Services.AddSingleton(_batchService);
    }

    [Fact]
    public async Task Progress_WithPartialCompletion_ShowsPercentageLabel()
    {
        var batch = new SimulationBatchResponse
        {
            Id = _batchId,
            EventId = Guid.NewGuid(),
            RunsRequested = 100,
            Seed = "test-seed",
            ExecutionMode = ExecutionMode.Local,
            Status = BatchStatus.Running
        };
        var progress = new BatchProgressResponse
        {
            Completed = 50,
            Failed = 0,
            Running = 0,
            Pending = 50,
            RetryCount = 0,
            ElapsedSeconds = 10,
            RunsPerSecond = 5
        };

        _batchService.GetBatchByIdAsync(_batchId, Arg.Any<CancellationToken>()).Returns(batch);
        _batchService.GetProgressAsync(_batchId, Arg.Any<CancellationToken>()).Returns(progress);
        _batchService.GetBatchAggregatesAsync(_batchId, Arg.Any<CancellationToken>()).Returns(Array.Empty<BatchTeamAggregateResponse>());

        var cut = Render<SimulationResults>(parameters => parameters
            .Add(p => p.BatchId, _batchId));

        await cut.InvokeAsync(() => { });
        cut.Render();

        cut.Find(".progress-bar-label").TextContent.Should().Contain("50% complete");
    }
}
