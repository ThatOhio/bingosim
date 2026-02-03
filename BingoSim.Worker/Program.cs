using BingoSim.Application.Interfaces;
using BingoSim.Infrastructure;
using BingoSim.Infrastructure.Hosting;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Simulation;
using BingoSim.Worker.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BingoSim.Worker;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

        builder.Services.AddInfrastructure(builder.Configuration);

        var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? builder.Configuration["RABBITMQ_HOST"] ?? "localhost";
        var rabbitPort = int.TryParse(builder.Configuration["RabbitMQ:Port"] ?? builder.Configuration["RABBITMQ_PORT"], out var p) ? p : 5672;
        var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? builder.Configuration["RABBITMQ_USER"] ?? "guest";
        var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? builder.Configuration["RABBITMQ_PASS"] ?? "guest";
        var rabbitUri = new Uri($"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:{rabbitPort}/");

        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<ExecuteSimulationRunConsumer>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitUri);
                cfg.UseMessageRetry(r => r.None());
                cfg.ConfigureEndpoints(context, new DefaultEndpointNameFormatter("bingosim-", false));
            });
        });

        builder.Services.AddScoped<MassTransitRunWorkPublisher>();
        builder.Services.AddScoped<ISimulationRunWorkPublisher>(sp => sp.GetRequiredService<MassTransitRunWorkPublisher>());

        builder.Services.Configure<WorkerSimulationOptions>(
            builder.Configuration.GetSection(WorkerSimulationOptions.SectionName));

        builder.Services.AddHostedService<BatchFinalizerHostedService>();

        var host = builder.Build();

        if (builder.Environment.IsDevelopment())
        {
            using var scope = host.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await EnsureMigrationsHistoryTableExistsAsync(dbContext);
            await dbContext.Database.MigrateAsync();
        }

        await host.RunAsync();
    }

    private static async Task EnsureMigrationsHistoryTableExistsAsync(AppDbContext dbContext)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory__" PRIMARY KEY ("MigrationId")
            );
            """;
        await dbContext.Database.ExecuteSqlRawAsync(sql);
    }
}
