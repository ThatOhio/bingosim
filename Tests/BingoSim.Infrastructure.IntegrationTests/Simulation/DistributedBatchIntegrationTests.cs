using System.Collections.Generic;
using System.Text.Json;
using BingoSim.Application.Interfaces;
using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Infrastructure;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Simulation;
using BingoSim.Shared.Messages;
using BingoSim.Worker.Consumers;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace BingoSim.Infrastructure.IntegrationTests.Simulation;

public class DistributedBatchIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private IServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private AppDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        var connectionString = _postgresContainer.GetConnectionString();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        services.AddScoped<MassTransitRunWorkPublisher>();
        services.AddScoped<ISimulationRunWorkPublisher>(sp => sp.GetRequiredService<MassTransitRunWorkPublisher>());
        services.Configure<WorkerSimulationOptions>(o => o.SimulationDelayMs = 0);

        services.AddMassTransitTestHarness(x => x.AddConsumer<ExecuteSimulationRunConsumer>());

        _provider = services.BuildServiceProvider();
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();

        _context = _provider.GetRequiredService<AppDbContext>();
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _context.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task DistributedBatch_FiveRuns_CompletesWithAggregates()
    {
        using var scope = _provider.CreateScope();
        var snapshotJson = BuildMinimalSnapshotJson();
        var batch = new SimulationBatch(Guid.NewGuid(), 5, "dist-test-seed", ExecutionMode.Distributed);
        var batchRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationBatchRepository>();
        var runRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.IEventSnapshotRepository>();

        await batchRepo.AddAsync(batch);
        var eventSnapshot = new EventSnapshot(batch.Id, snapshotJson);
        await snapshotRepo.AddAsync(eventSnapshot);

        var runs = new List<SimulationRun>();
        for (var i = 0; i < 5; i++)
        {
            var seed = SeedDerivation.DeriveRunSeedString("dist-test-seed", i);
            runs.Add(new SimulationRun(batch.Id, i, seed));
        }
        await runRepo.AddRangeAsync(runs);

        foreach (var run in runs)
        {
            await _harness.Bus.Publish(new ExecuteSimulationRun { SimulationRunId = run.Id });
        }

        // Poll for completion using fresh scopes to avoid EF Core caching
        for (var i = 0; i < 60; i++)
        {
            using var pollScope = _provider.CreateScope();
            var pollRunRepo = pollScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
            var completed = await pollRunRepo.GetByBatchIdAsync(batch.Id);
            if (completed.All(r => r.Status == RunStatus.Completed))
                break;
            await Task.Delay(500);
        }

        // Use fresh scope for verification to get fresh data from the database
        using var verifyScope = _provider.CreateScope();
        var verifyRunRepo = verifyScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var finalizationService = verifyScope.ServiceProvider.GetRequiredService<IBatchFinalizationService>();
        await finalizationService.TryFinalizeAsync(batch.Id);

        var completedRuns = await verifyRunRepo.GetByBatchIdAsync(batch.Id);
        completedRuns.Should().HaveCount(5);
        completedRuns.Should().OnlyContain(r => r.Status == RunStatus.Completed);

        var aggregateRepo = verifyScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.IBatchTeamAggregateRepository>();
        var aggregates = await aggregateRepo.GetByBatchIdAsync(batch.Id);
        aggregates.Should().NotBeEmpty();
        aggregates.Should().OnlyContain(a => a.RunCount == 5);
    }

    [Fact]
    public async Task TerminalFailure_AllRunsTerminalWithOneFailed_MarksBatchErrorWithErrorMessage()
    {
        using var scope = _provider.CreateScope();
        var batch = new SimulationBatch(Guid.NewGuid(), 3, "error-test-seed", ExecutionMode.Distributed);
        var batchRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationBatchRepository>();
        var runRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var finalizationService = scope.ServiceProvider.GetRequiredService<IBatchFinalizationService>();

        batch.SetStatus(BatchStatus.Running);
        await batchRepo.AddAsync(batch);

        var runs = new List<SimulationRun>();
        for (var i = 0; i < 3; i++)
        {
            var seed = SeedDerivation.DeriveRunSeedString("error-test-seed", i);
            runs.Add(new SimulationRun(batch.Id, i, seed));
        }
        await runRepo.AddRangeAsync(runs);

        for (var i = 0; i < 5; i++)
            runs[0].MarkFailed("Simulated terminal error", DateTimeOffset.UtcNow);
        runs[1].MarkCompleted(DateTimeOffset.UtcNow);
        runs[2].MarkCompleted(DateTimeOffset.UtcNow);
        foreach (var r in runs)
            await runRepo.UpdateAsync(r);

        var finalized = await finalizationService.TryFinalizeAsync(batch.Id);
        finalized.Should().BeTrue();

        using var verifyScope = _provider.CreateScope();
        var verifyBatchRepo = verifyScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationBatchRepository>();
        var finalBatch = await verifyBatchRepo.GetByIdAsync(batch.Id);
        finalBatch.Should().NotBeNull();
        finalBatch!.Status.Should().Be(BatchStatus.Error);
        finalBatch.ErrorMessage.Should().NotBeNullOrEmpty();
        finalBatch.ErrorMessage.Should().Contain("failed");
    }

    [Fact]
    public async Task BatchFinalization_SecondCall_ReturnsFalseIdempotent()
    {
        using var scope = _provider.CreateScope();
        var batch = new SimulationBatch(Guid.NewGuid(), 2, "idempotent-seed", ExecutionMode.Distributed);
        var batchRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationBatchRepository>();
        var runRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var finalizationService = scope.ServiceProvider.GetRequiredService<IBatchFinalizationService>();

        batch.SetStatus(BatchStatus.Running);
        await batchRepo.AddAsync(batch);

        var runs = new List<SimulationRun>();
        for (var i = 0; i < 2; i++)
        {
            var seed = SeedDerivation.DeriveRunSeedString("idempotent-seed", i);
            runs.Add(new SimulationRun(batch.Id, i, seed));
        }
        await runRepo.AddRangeAsync(runs);
        runs[0].MarkCompleted(DateTimeOffset.UtcNow);
        runs[1].MarkCompleted(DateTimeOffset.UtcNow);
        foreach (var r in runs)
            await runRepo.UpdateAsync(r);

        var first = await finalizationService.TryFinalizeAsync(batch.Id);
        first.Should().BeTrue();

        var second = await finalizationService.TryFinalizeAsync(batch.Id);
        second.Should().BeFalse();
    }

    private static string BuildMinimalSnapshotJson()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var dto = new EventSnapshotDto
        {
            EventName = "Minimal",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [] }] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [] }] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [] }] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [] }] }
                    ]
                }
            ],
            ActivitiesById = new Dictionary<Guid, ActivitySnapshotDto>
            {
                [actId] = new ActivitySnapshotDto
                {
                    Id = actId,
                    Key = "act",
                    Attempts =
                    [
                        new AttemptSnapshotDto
                        {
                            Key = "main",
                            RollScope = 0,
                            BaselineTimeSeconds = 60,
                            VarianceSeconds = 10,
                            Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }]
                        }
                    ],
                    GroupScalingBands = []
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = teamId,
                    TeamName = "Team A",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = playerId, Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [] }]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }
}
