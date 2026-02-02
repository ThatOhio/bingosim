using BingoSim.Application.Interfaces;
using BingoSim.Infrastructure;
using BingoSim.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BingoSim.Seed;

public static class Program
{
    private const string FullResetConfirmationWord = "yes";

    public static async Task<int> Main(string[] args)
    {
        var reset = args.Contains("--reset", StringComparer.OrdinalIgnoreCase);
        var fullReset = args.Contains("--full-reset", StringComparer.OrdinalIgnoreCase);
        var confirm = args.Contains("--confirm", StringComparer.OrdinalIgnoreCase);

        var host = Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((context, services) =>
            {
                services.AddInfrastructure(context.Configuration);
            })
            .Build();

        // Ensure database is migrated
        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        if (fullReset)
        {
            if (!confirm && !await PromptConfirmFullResetAsync())
            {
                Console.WriteLine("Full reset cancelled.");
                return 1;
            }

            using (var scope = host.Services.CreateScope())
            {
                var fullResetService = scope.ServiceProvider.GetRequiredService<IFullDatabaseResetService>();
                await fullResetService.ResetAllDataAsync();
            }

            Console.WriteLine("Full database reset: done.");
            return 0;
        }

        using (var scope = host.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<IDevSeedService>();

            if (reset)
            {
                Console.WriteLine("Dev seed: reset and reseed...");
                await seeder.ResetAndSeedAsync();
            }
            else
            {
                Console.WriteLine("Dev seed: idempotent seed...");
                await seeder.SeedAsync();
            }
        }

        Console.WriteLine("Dev seed: done.");
        return 0;
    }

    /// <summary>
    /// Prompts the user to type 'yes' to confirm. Returns true only if the exact word is entered.
    /// </summary>
    private static async Task<bool> PromptConfirmFullResetAsync()
    {
        Console.WriteLine();
        Console.WriteLine("*** WARNING: This will PERMANENTLY delete ALL data in the database. ***");
        Console.WriteLine("    (Players, Activities, Events, Teams, Simulation batches, results, etc.)");
        Console.WriteLine();
        Console.Write($"Type '{FullResetConfirmationWord}' to confirm: ");

        var line = await Console.In.ReadLineAsync();
        return string.Equals(line?.Trim(), FullResetConfirmationWord, StringComparison.OrdinalIgnoreCase);
    }
}
