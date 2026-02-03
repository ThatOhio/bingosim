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

public class BatchesTests : BunitContext
{
    private readonly ISimulationBatchService _batchService = Substitute.For<ISimulationBatchService>();

    public BatchesTests()
    {
        Services.AddSingleton(_batchService);
    }

    [Fact]
    public async Task InitialLoad_ShowsLoadingSpinner_ThenShowsContent()
    {
        var tcs = new TaskCompletionSource<ListBatchesResult>();
        var batch = new BatchListRowDto
        {
            BatchId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = BatchStatus.Completed,
            EventName = "Test Event",
            RunCount = 100,
            CompletedCount = 100,
            FailedCount = 0,
            Seed = "abc",
            ExecutionMode = ExecutionMode.Local
        };
        _batchService.GetBatchesAsync(Arg.Any<ListBatchesRequest>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        var cut = Render<Batches>();

        cut.Find(".loading-spinner").Should().NotBeNull();

        tcs.SetResult(new ListBatchesResult { Items = [batch] });
        await cut.InvokeAsync(() => { });
        cut.Render();

        cut.Find(".data-table").Should().NotBeNull();
        cut.FindAll(".data-table tbody tr").Should().HaveCount(1);
    }

    [Fact]
    public async Task ApplyFilters_DoesNotReplaceTableWithBlockSpinner()
    {
        var batch = new BatchListRowDto
        {
            BatchId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = BatchStatus.Completed,
            EventName = "Test Event",
            RunCount = 100,
            CompletedCount = 100,
            FailedCount = 0,
            Seed = "abc",
            ExecutionMode = ExecutionMode.Local
        };
        _batchService.GetBatchesAsync(Arg.Any<ListBatchesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListBatchesResult { Items = [batch] });

        var cut = Render<Batches>();

        await cut.InvokeAsync(() => { });
        cut.Render();

        var applyButton = cut.Find("button[type='button']");

        applyButton.Click();

        cut.Render();

        var tableAfter = cut.Find(".data-table");
        tableAfter.Should().NotBeNull();
        cut.FindAll(".data-table tbody tr").Should().HaveCount(1);
    }

    [Fact]
    public async Task ServiceThrows_ShowsAlert()
    {
        _batchService.GetBatchesAsync(Arg.Any<ListBatchesRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ListBatchesResult>(new InvalidOperationException("Service unavailable")));

        var cut = Render<Batches>();

        await cut.InvokeAsync(() => { });
        cut.Render();

        cut.Find(".alert").TextContent.Trim().Should().Contain("Service unavailable");
    }

    [Fact]
    public async Task ManualRefresh_WithActiveBatches_InvokesLoadOnce()
    {
        var batch = new BatchListRowDto
        {
            BatchId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = BatchStatus.Running,
            EventName = "Test Event",
            RunCount = 100,
            CompletedCount = 50,
            FailedCount = 0,
            Seed = "abc",
            ExecutionMode = ExecutionMode.Local
        };
        _batchService.GetBatchesAsync(Arg.Any<ListBatchesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListBatchesResult { Items = [batch] });

        var cut = Render<Batches>();

        await cut.InvokeAsync(() => { });
        cut.Render();

        await _batchService.Received(1).GetBatchesAsync(Arg.Any<ListBatchesRequest>(), Arg.Any<CancellationToken>());

        var refreshButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Refresh"));
        refreshButton.Should().NotBeNull();
        refreshButton!.Click();

        await cut.InvokeAsync(() => { });
        cut.Render();

        await _batchService.Received(2).GetBatchesAsync(Arg.Any<ListBatchesRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pause_WithActiveBatches_ShowsResumeButton()
    {
        var batch = new BatchListRowDto
        {
            BatchId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = BatchStatus.Running,
            EventName = "Test Event",
            RunCount = 100,
            CompletedCount = 50,
            FailedCount = 0,
            Seed = "abc",
            ExecutionMode = ExecutionMode.Local
        };
        _batchService.GetBatchesAsync(Arg.Any<ListBatchesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListBatchesResult { Items = [batch] });

        var cut = Render<Batches>();

        await cut.InvokeAsync(() => { });
        cut.Render();

        var pauseButton = cut.FindAll("button").First(b => b.TextContent.Contains("Pause"));
        pauseButton.Click();

        await cut.InvokeAsync(() => { });
        cut.Render();

        cut.FindAll("button").First(b => b.TextContent.Contains("Resume")).Should().NotBeNull();
    }

    [Fact]
    public async Task Resume_AfterPause_ShowsPauseButton()
    {
        var batch = new BatchListRowDto
        {
            BatchId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = BatchStatus.Running,
            EventName = "Test Event",
            RunCount = 100,
            CompletedCount = 50,
            FailedCount = 0,
            Seed = "abc",
            ExecutionMode = ExecutionMode.Local
        };
        _batchService.GetBatchesAsync(Arg.Any<ListBatchesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListBatchesResult { Items = [batch] });

        var cut = Render<Batches>();

        await cut.InvokeAsync(() => { });
        cut.Render();

        cut.FindAll("button").First(b => b.TextContent.Contains("Pause")).Click();
        await cut.InvokeAsync(() => { });
        cut.Render();

        cut.FindAll("button").First(b => b.TextContent.Contains("Resume")).Click();
        await cut.InvokeAsync(() => { });
        cut.Render();

        cut.FindAll("button").First(b => b.TextContent.Contains("Pause")).Should().NotBeNull();
    }

    [Fact]
    public async Task EmptyState_ShowsRunSimulationsCta()
    {
        _batchService.GetBatchesAsync(Arg.Any<ListBatchesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListBatchesResult { Items = [] });

        var cut = Render<Batches>();

        await cut.InvokeAsync(() => { });
        cut.Render();

        var ctaLink = cut.Find(".empty-state a[href='/simulations/run']");
        ctaLink.TextContent.Trim().Should().Be("Run Simulations");
    }

    [Fact]
    public async Task LastUpdated_AfterLoad_ShowsTimestamp()
    {
        var batch = new BatchListRowDto
        {
            BatchId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = BatchStatus.Running,
            EventName = "Test Event",
            RunCount = 100,
            CompletedCount = 50,
            FailedCount = 0,
            Seed = "abc",
            ExecutionMode = ExecutionMode.Local
        };
        _batchService.GetBatchesAsync(Arg.Any<ListBatchesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListBatchesResult { Items = [batch] });

        var cut = Render<Batches>();

        await cut.InvokeAsync(() => { });
        cut.Render();

        cut.Find(".refresh-indicator__last-updated").TextContent.Should().Contain("Last updated:");
        cut.Find(".refresh-indicator__last-updated").TextContent.Should().NotContain("—");
    }
}
