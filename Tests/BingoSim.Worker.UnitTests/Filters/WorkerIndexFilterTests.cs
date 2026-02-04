using BingoSim.Shared.Messages;
using BingoSim.Worker.Filters;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace BingoSim.Worker.UnitTests.Filters;

public class WorkerIndexFilterTests
{
    private static ILogger<WorkerIndexFilter> CreateLogger() =>
        Substitute.For<ILogger<WorkerIndexFilter>>();

    private static ConsumeContext<ExecuteSimulationRunBatch> CreateContext(
        IReadOnlyList<Guid> runIds,
        int? workerIndex = null)
    {
        var message = new ExecuteSimulationRunBatch
        {
            SimulationRunIds = runIds,
            WorkerIndex = workerIndex
        };
        var context = Substitute.For<ConsumeContext<ExecuteSimulationRunBatch>>();
        context.Message.Returns(message);
        return context;
    }

    private static IPipe<ConsumeContext<ExecuteSimulationRunBatch>> CreatePipe()
    {
        var pipe = Substitute.For<IPipe<ConsumeContext<ExecuteSimulationRunBatch>>>();
        pipe.Send(Arg.Any<ConsumeContext<ExecuteSimulationRunBatch>>()).Returns(Task.CompletedTask);
        return pipe;
    }

    [Fact]
    public async Task Send_NoWorkerIndexConfigured_ProcessesAllMessages()
    {
        var logger = CreateLogger();
        var filter = new WorkerIndexFilter(null, 3, logger);
        var context = CreateContext([Guid.NewGuid()], 0);
        var pipe = CreatePipe();

        await filter.Send(context, pipe);

        await pipe.Received(1).Send(Arg.Any<ConsumeContext<ExecuteSimulationRunBatch>>());
    }

    [Fact]
    public async Task Send_MessageHasNoWorkerIndex_ProcessesMessage()
    {
        var logger = CreateLogger();
        var filter = new WorkerIndexFilter(0, 3, logger);
        var context = CreateContext([Guid.NewGuid()], null);
        var pipe = CreatePipe();

        await filter.Send(context, pipe);

        await pipe.Received(1).Send(Arg.Any<ConsumeContext<ExecuteSimulationRunBatch>>());
    }

    [Fact]
    public async Task Send_WorkerIndexMatches_ProcessesMessage()
    {
        var logger = CreateLogger();
        var filter = new WorkerIndexFilter(1, 3, logger);
        var context = CreateContext([Guid.NewGuid()], 1);
        var pipe = CreatePipe();

        await filter.Send(context, pipe);

        await pipe.Received(1).Send(Arg.Any<ConsumeContext<ExecuteSimulationRunBatch>>());
    }

    [Fact]
    public async Task Send_WorkerIndexMismatch_SkipsMessage()
    {
        var logger = CreateLogger();
        var filter = new WorkerIndexFilter(0, 3, logger);
        var context = CreateContext([Guid.NewGuid()], 1);
        context.NotifyConsumed(Arg.Any<ConsumeContext<ExecuteSimulationRunBatch>>(), Arg.Any<TimeSpan>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var pipe = CreatePipe();

        await filter.Send(context, pipe);

        await pipe.DidNotReceive().Send(Arg.Any<ConsumeContext<ExecuteSimulationRunBatch>>());
    }

    [Fact]
    public async Task Send_WorkerIndexOutOfBounds_ProcessesAnywayToAvoidLoss()
    {
        var logger = CreateLogger();
        var filter = new WorkerIndexFilter(5, 3, logger); // 5 >= 3, out of bounds
        var context = CreateContext([Guid.NewGuid()], 0);
        var pipe = CreatePipe();

        await filter.Send(context, pipe);

        await pipe.Received(1).Send(Arg.Any<ConsumeContext<ExecuteSimulationRunBatch>>());
    }

}
