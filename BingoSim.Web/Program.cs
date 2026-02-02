using BingoSim.Application.Validators;
using BingoSim.Infrastructure;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Web.Components;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Add Infrastructure services (DbContext, Repositories, Application Services)
        builder.Services.AddInfrastructure(builder.Configuration);

        builder.Services.Configure<BingoSim.Web.Services.LocalSimulationOptions>(
            builder.Configuration.GetSection(BingoSim.Web.Services.LocalSimulationOptions.SectionName));
        builder.Services.AddHostedService<BingoSim.Web.Services.SimulationRunQueueHostedService>();

        // Add FluentValidation validators
        builder.Services.AddValidatorsFromAssemblyContaining<CreatePlayerProfileRequestValidator>();

        var app = builder.Build();

        // Apply migrations automatically in development
        if (app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await EnsureMigrationsHistoryTableExistsAsync(dbContext);
            await dbContext.Database.MigrateAsync();
        }

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        await app.RunAsync();
    }

    /// <summary>
    /// Ensures the EF Core migrations history table exists. Required on PostgreSQL when the database
    /// has never had migrations applied (Npgsql tries to query the table before creating it).
    /// </summary>
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