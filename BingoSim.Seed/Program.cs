using BingoSim.Application.Interfaces;
using BingoSim.Infrastructure;
using BingoSim.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BingoSim.Seed;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var reset = args.Contains("--reset", StringComparer.OrdinalIgnoreCase);

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
}
