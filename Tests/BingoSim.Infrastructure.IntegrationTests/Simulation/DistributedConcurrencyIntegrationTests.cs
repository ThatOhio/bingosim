using System.Collections.Generic;
using System.Text.Json;
using BingoSim.Application.Interfaces;
using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Infrastructure;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Simulation;
using BingoSim.Shared.Messages;
using BingoSim.Worker.Consumers;
using BingoSim.Worker.Services;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace BingoSim.Infrastructure.IntegrationTests.Simulation;

/// <summary>
/// Verifies that multiple ExecuteSimulationRunBatch messages are processed concurrently when MaxConcurrentRuns > 1.
/// Uses IWorkerConcurrencyObserver to avoid flaky timing assertions.
/// </summary>
public class DistributedConcurrencyIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private IServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private AppDbContext _context = null!;
    private TestConcurrencyObserver _concurrencyObserver = null!;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _concurrencyObserver = new TestConcurrencyObserver();

        var connectionString = _postgresContainer.GetConnectionString();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["SimulationPersistence:BatchSize"] = "1",
                ["DistributedExecution:BatchSize"] = "10"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        services.AddScoped<MassTransitRunWorkPublisher>();
        services.AddScoped<ISimulationRunWorkPublisher>(sp => sp.GetRequiredService<MassTransitRunWorkPublisher>());
        services.Configure<WorkerSimulationOptions>(o =>
        {
            o.SimulationDelayMs = 100;
            o.MaxConcurrentRuns = 4;
        });
        services.AddSingleton<IPerfRecorder, PerfRecorder>();
        services.AddSingleton<IWorkerRunThroughputRecorder, WorkerRunThroughputRecorder>();
        services.AddSingleton<IWorkerConcurrencyObserver>(_concurrencyObserver);

        services.AddMassTransitTestHarness(x =>
            x.AddConsumer<ExecuteSimulationRunBatchConsumer, ExecuteSimulationRunBatchConsumerDefinition>());

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
    public async Task DistributedBatch_MultipleMessages_ProcessedConcurrently()
    {
        using var scope = _provider.CreateScope();
        var snapshotJson = BuildMinimalSnapshotJson();
        var batch = new SimulationBatch(Guid.NewGuid(), 4, "concurrency-test-seed", ExecutionMode.Distributed);
        var batchRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationBatchRepository>();
        var runRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.IEventSnapshotRepository>();

        await batchRepo.AddAsync(batch);
        var eventSnapshot = new EventSnapshot(batch.Id, snapshotJson);
        await snapshotRepo.AddAsync(eventSnapshot);

        var runs = new List<SimulationRun>();
        for (var i = 0; i < 4; i++)
        {
            var seed = SeedDerivation.DeriveRunSeedString("concurrency-test-seed", i);
            runs.Add(new SimulationRun(batch.Id, i, seed));
        }
        await runRepo.AddRangeAsync(runs);

        // Publish 4 batch messages (1 run each) to exercise concurrent batch processing
        foreach (var run in runs)
        {
            await _harness.Bus.Publish(new ExecuteSimulationRunBatch { SimulationRunIds = [run.Id] });
        }

        // Poll until we observe >= 2 concurrent in-progress, or all complete
        for (var i = 0; i < 60; i++)
        {
            var inProgress = _concurrencyObserver.CurrentInProgress;
            if (inProgress >= 2)
                break;

            using var pollScope = _provider.CreateScope();
            var pollRunRepo = pollScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
            var completed = await pollRunRepo.GetByBatchIdAsync(batch.Id);
            if (completed.All(r => r.Status == RunStatus.Completed))
                break;

            await Task.Delay(50);
        }

        // Wait for all to complete
        for (var i = 0; i < 60; i++)
        {
            using var pollScope = _provider.CreateScope();
            var pollRunRepo = pollScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
            var completed = await pollRunRepo.GetByBatchIdAsync(batch.Id);
            if (completed.All(r => r.Status == RunStatus.Completed))
                break;
            await Task.Delay(200);
        }

        using var verifyScope = _provider.CreateScope();
        var verifyRunRepo = verifyScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var finalizationService = verifyScope.ServiceProvider.GetRequiredService<IBatchFinalizationService>();
        await finalizationService.TryFinalizeAsync(batch.Id);

        var completedRuns = await verifyRunRepo.GetByBatchIdAsync(batch.Id);
        completedRuns.Should().HaveCount(4);
        completedRuns.Should().OnlyContain(r => r.Status == RunStatus.Completed);

        _concurrencyObserver.MaxConcurrentObserved.Should().BeGreaterThanOrEqualTo(2,
            "at least 2 messages should have been in progress concurrently (MaxConcurrentRuns=4)");
    }

    private static string BuildMinimalSnapshotJson()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var dto = new EventSnapshotDto
        {
            EventName = "Minimal",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = eventStart.ToString("o"),
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [], Modifiers = [] }] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [], Modifiers = [] }] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [], Modifiers = [] }] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [], Modifiers = [] }] }
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
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = teamId,
                    TeamName = "Team A",
                    StrategyKey = "RowUnlocking",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = playerId, Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    private sealed class TestConcurrencyObserver : IWorkerConcurrencyObserver
    {
        private int _inProgress;
        private int _maxObserved;

        public int CurrentInProgress => Volatile.Read(ref _inProgress);

        public int MaxConcurrentObserved => Volatile.Read(ref _maxObserved);

        public void OnConsumeStarted()
        {
            var current = Interlocked.Increment(ref _inProgress);
            var max = Volatile.Read(ref _maxObserved);
            while (current > max)
            {
                var prev = Interlocked.CompareExchange(ref _maxObserved, current, max);
                if (prev == max) break;
                max = prev;
            }
        }

        public void OnConsumeEnded() => Interlocked.Decrement(ref _inProgress);
    }
}
