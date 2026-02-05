using System.Collections.Generic;
using System.Text.Json;
using BingoSim.Application.Interfaces;
using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Infrastructure;
using BingoSim.Infrastructure.IntegrationTests.Fixtures;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Simulation;
using BingoSim.Shared.Messages;
using BingoSim.Worker.Consumers;
using BingoSim.Worker.Services;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BingoSim.Infrastructure.IntegrationTests.Simulation;

[Collection("Postgres")]
public class DistributedBatchIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private AppDbContext _context = null!;

    public DistributedBatchIntegrationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        var connectionString = await _postgres.CreateIsolatedDatabaseAsync(GetType().Name);
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
        services.Configure<WorkerSimulationOptions>(o => o.SimulationDelayMs = 0);
        services.AddSingleton<IPerfRecorder, PerfRecorder>();
        services.AddSingleton<IWorkerRunThroughputRecorder, WorkerRunThroughputRecorder>();

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

        await _harness.Bus.Publish(new ExecuteSimulationRunBatch { SimulationRunIds = runs.Select(r => r.Id).ToList() });

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
    public async Task DistributedBatch_PublishRunWorkBatchAsync_CompletesWithAggregates()
    {
        using var scope = _provider.CreateScope();
        var snapshotJson = BuildMinimalSnapshotJson();
        var batch = new SimulationBatch(Guid.NewGuid(), 3, "batch-publish-seed", ExecutionMode.Distributed);
        var batchRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationBatchRepository>();
        var runRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.IEventSnapshotRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<MassTransitRunWorkPublisher>();

        await batchRepo.AddAsync(batch);
        var eventSnapshot = new EventSnapshot(batch.Id, snapshotJson);
        await snapshotRepo.AddAsync(eventSnapshot);

        var runs = new List<SimulationRun>();
        for (var i = 0; i < 3; i++)
        {
            var seed = SeedDerivation.DeriveRunSeedString("batch-publish-seed", i);
            runs.Add(new SimulationRun(batch.Id, i, seed));
        }
        await runRepo.AddRangeAsync(runs);

        var runIds = runs.Select(r => r.Id).ToList();
        await publisher.PublishRunWorkBatchAsync(runIds); // Publishes ExecuteSimulationRunBatch (chunked by BatchSize)

        for (var i = 0; i < 60; i++)
        {
            using var pollScope = _provider.CreateScope();
            var pollRunRepo = pollScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
            var completed = await pollRunRepo.GetByBatchIdAsync(batch.Id);
            if (completed.All(r => r.Status == RunStatus.Completed))
                break;
            await Task.Delay(500);
        }

        using var verifyScope = _provider.CreateScope();
        var verifyRunRepo = verifyScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var finalizationService = verifyScope.ServiceProvider.GetRequiredService<IBatchFinalizationService>();
        await finalizationService.TryFinalizeAsync(batch.Id);

        var completedRuns = await verifyRunRepo.GetByBatchIdAsync(batch.Id);
        completedRuns.Should().HaveCount(3);
        completedRuns.Should().OnlyContain(r => r.Status == RunStatus.Completed);
    }

    [Fact]
    public async Task DistributedBatch_SnapshotWithModifiers_CompletesSuccessfully()
    {
        using var scope = _provider.CreateScope();
        var snapshotJson = BuildSnapshotWithModifiers();
        var batch = new SimulationBatch(Guid.NewGuid(), 3, "modifier-dist-seed", ExecutionMode.Distributed);
        var batchRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationBatchRepository>();
        var runRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.IEventSnapshotRepository>();

        await batchRepo.AddAsync(batch);
        var eventSnapshot = new EventSnapshot(batch.Id, snapshotJson);
        await snapshotRepo.AddAsync(eventSnapshot);

        var runs = new List<SimulationRun>();
        for (var i = 0; i < 3; i++)
        {
            var seed = SeedDerivation.DeriveRunSeedString("modifier-dist-seed", i);
            runs.Add(new SimulationRun(batch.Id, i, seed));
        }
        await runRepo.AddRangeAsync(runs);

        await _harness.Bus.Publish(new ExecuteSimulationRunBatch { SimulationRunIds = runs.Select(r => r.Id).ToList() });

        for (var i = 0; i < 60; i++)
        {
            using var pollScope = _provider.CreateScope();
            var pollRunRepo = pollScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
            var completed = await pollRunRepo.GetByBatchIdAsync(batch.Id);
            if (completed.All(r => r.Status == RunStatus.Completed))
                break;
            await Task.Delay(500);
        }

        using var verifyScope = _provider.CreateScope();
        var verifyRunRepo = verifyScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var finalizationService = verifyScope.ServiceProvider.GetRequiredService<IBatchFinalizationService>();
        await finalizationService.TryFinalizeAsync(batch.Id);

        var completedRuns = await verifyRunRepo.GetByBatchIdAsync(batch.Id);
        completedRuns.Should().HaveCount(3);
        completedRuns.Should().OnlyContain(r => r.Status == RunStatus.Completed);
    }

    [Fact]
    public async Task DistributedBatch_GroupSnapshotFields_PresentAndUsed_CompletesSuccessfully()
    {
        using var scope = _provider.CreateScope();
        var snapshotJson = BuildSnapshotWithGroupFields();
        var batch = new SimulationBatch(Guid.NewGuid(), 2, "group-dist-seed", ExecutionMode.Distributed);
        var batchRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationBatchRepository>();
        var runRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.IEventSnapshotRepository>();

        await batchRepo.AddAsync(batch);
        var eventSnapshot = new EventSnapshot(batch.Id, snapshotJson);
        await snapshotRepo.AddAsync(eventSnapshot);

        var runs = new List<SimulationRun>();
        for (var i = 0; i < 2; i++)
        {
            var seed = SeedDerivation.DeriveRunSeedString("group-dist-seed", i);
            runs.Add(new SimulationRun(batch.Id, i, seed));
        }
        await runRepo.AddRangeAsync(runs);

        await _harness.Bus.Publish(new ExecuteSimulationRunBatch { SimulationRunIds = runs.Select(r => r.Id).ToList() });

        for (var i = 0; i < 60; i++)
        {
            using var pollScope = _provider.CreateScope();
            var pollRunRepo = pollScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
            var completed = await pollRunRepo.GetByBatchIdAsync(batch.Id);
            if (completed.All(r => r.Status == RunStatus.Completed))
                break;
            await Task.Delay(500);
        }

        using var verifyScope = _provider.CreateScope();
        var verifyRunRepo = verifyScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var finalizationService = verifyScope.ServiceProvider.GetRequiredService<IBatchFinalizationService>();
        await finalizationService.TryFinalizeAsync(batch.Id);

        var completedRuns = await verifyRunRepo.GetByBatchIdAsync(batch.Id);
        completedRuns.Should().HaveCount(2);
        completedRuns.Should().OnlyContain(r => r.Status == RunStatus.Completed);
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
        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new BingoSim.Application.Simulation.Schedule.WeeklyScheduleSnapshotDto { Sessions = [] };

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

    private static string BuildSnapshotWithModifiers()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var ruleWithModifier = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = [new ActivityModifierRuleSnapshotDto { CapabilityKey = "quest.ds2", TimeMultiplier = 0.9m, ProbabilityMultiplier = 1.1m }]
        };

        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new BingoSim.Application.Simulation.Schedule.WeeklyScheduleSnapshotDto { Sessions = [] };

        var dto = new EventSnapshotDto
        {
            EventName = "Modifier Dist Test",
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
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [ruleWithModifier] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [ruleWithModifier] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [ruleWithModifier] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 1, AllowedActivities = [ruleWithModifier] }
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
                    Players = [new PlayerSnapshotDto { PlayerId = playerId, Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = ["quest.ds2"], Schedule = alwaysOnline }]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    private static string BuildSnapshotWithGroupFields()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new BingoSim.Application.Simulation.Schedule.WeeklyScheduleSnapshotDto { Sessions = [] };

        var dto = new EventSnapshotDto
        {
            EventName = "Group Dist Test",
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
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }
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
                    GroupScalingBands =
                    [
                        new GroupSizeBandSnapshotDto { MinSize = 1, MaxSize = 1, TimeMultiplier = 1.0m, ProbabilityMultiplier = 1.0m },
                        new GroupSizeBandSnapshotDto { MinSize = 2, MaxSize = 4, TimeMultiplier = 0.9m, ProbabilityMultiplier = 1.1m }
                    ],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = true, MinGroupSize = 2, MaxGroupSize = 4 }
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
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = p1, Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline },
                        new PlayerSnapshotDto { PlayerId = p2, Name = "P2", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    [Fact]
    public async Task DistributedBatch_WithScheduleSnapshot_CompletesSuccessfully()
    {
        using var scope = _provider.CreateScope();
        var snapshotJson = BuildSnapshotWithSchedule();
        var batch = new SimulationBatch(Guid.NewGuid(), 3, "schedule-dist-seed", ExecutionMode.Distributed);
        var batchRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationBatchRepository>();
        var runRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.IEventSnapshotRepository>();

        await batchRepo.AddAsync(batch);
        var eventSnapshot = new EventSnapshot(batch.Id, snapshotJson);
        await snapshotRepo.AddAsync(eventSnapshot);

        var runs = new List<SimulationRun>();
        for (var i = 0; i < 3; i++)
        {
            var seed = SeedDerivation.DeriveRunSeedString("schedule-dist-seed", i);
            runs.Add(new SimulationRun(batch.Id, i, seed));
        }
        await runRepo.AddRangeAsync(runs);

        await _harness.Bus.Publish(new ExecuteSimulationRunBatch { SimulationRunIds = runs.Select(r => r.Id).ToList() });

        for (var i = 0; i < 60; i++)
        {
            using var pollScope = _provider.CreateScope();
            var pollRunRepo = pollScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
            var completed = await pollRunRepo.GetByBatchIdAsync(batch.Id);
            if (completed.All(r => r.Status == RunStatus.Completed))
                break;
            await Task.Delay(500);
        }

        using var verifyScope = _provider.CreateScope();
        var verifyRunRepo = verifyScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var finalizationService = verifyScope.ServiceProvider.GetRequiredService<IBatchFinalizationService>();
        await finalizationService.TryFinalizeAsync(batch.Id);

        var completedRuns = await verifyRunRepo.GetByBatchIdAsync(batch.Id);
        completedRuns.Should().HaveCount(3);
        completedRuns.Should().OnlyContain(r => r.Status == RunStatus.Completed);

        var aggregateRepo = verifyScope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.IBatchTeamAggregateRepository>();
        var aggregates = await aggregateRepo.GetByBatchIdAsync(batch.Id);
        aggregates.Should().NotBeEmpty();
    }

    private static string BuildSnapshotWithSchedule()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 120 }]
        };
        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));

        var dto = new EventSnapshotDto
        {
            EventName = "Schedule Dist Test",
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
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }
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
                            VarianceSeconds = 5,
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
                    TeamName = "Team",
                    StrategyKey = "RowUnlocking",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = playerId, Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = schedule }]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }
}
