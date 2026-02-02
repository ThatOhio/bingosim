using BingoSim.Application.Interfaces;
using BingoSim.Application.Services;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Core.Interfaces;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Persistence.Repositories;
using BingoSim.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BingoSim.Infrastructure;

/// <summary>
/// Extension methods for registering infrastructure services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Repositories
        services.AddScoped<IPlayerProfileRepository, PlayerProfileRepository>();
        services.AddScoped<IActivityDefinitionRepository, ActivityDefinitionRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ISimulationBatchRepository, SimulationBatchRepository>();
        services.AddScoped<IEventSnapshotRepository, EventSnapshotRepository>();
        services.AddScoped<ISimulationRunRepository, SimulationRunRepository>();
        services.AddScoped<ITeamRunResultRepository, TeamRunResultRepository>();
        services.AddScoped<IBatchTeamAggregateRepository, BatchTeamAggregateRepository>();

        // Simulation (Application)
        services.AddSingleton<IProgressAllocatorFactory, ProgressAllocatorFactory>();
        services.AddScoped<EventSnapshotBuilder>();
        services.AddScoped<SimulationRunner>();
        services.AddScoped<ISimulationRunExecutor, SimulationRunExecutor>();
        services.AddSingleton<ISimulationRunQueue, SimulationRunQueue>();
        services.AddScoped<ISimulationBatchService, SimulationBatchService>();

        // Application Services
        services.AddScoped<IPlayerProfileService, PlayerProfileService>();
        services.AddScoped<IActivityDefinitionService, ActivityDefinitionService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IDevSeedService, DevSeedService>();

        return services;
    }
}
