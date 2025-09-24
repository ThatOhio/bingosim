using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BingoSim.Config;
using BingoSim.Models;
using BingoSim.Simulation;
using BingoSim.Strategies;

namespace BingoSim;

class Program
{
    static void Main(string[] args)
    {
        // Args: --config <path> --runs <N> --strategy <greedy|grouped|unlocker> --seed <int> --threads <N>
        string configPath = Path.Combine(AppContext.BaseDirectory, "bingo-board.json");
        int runs = 1000;
        string strategyName = "unlocker"; // default strategy
        int? seed = null;
        int threads = Environment.ProcessorCount;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config":
                    if (i + 1 < args.Length) configPath = args[++i];
                    break;
                case "--runs":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var r)) runs = r;
                    break;
                case "--strategy":
                    if (i + 1 < args.Length) strategyName = args[++i];
                    break;
                case "--seed":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var s)) seed = s;
                    break;
                case "--threads":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var th)) threads = Math.Max(1, th);
                    break;
            }
        }

        if (!File.Exists(configPath))
        {
            Console.WriteLine($"Config not found at {configPath}. Using embedded sample.");
            configPath = Path.Combine(AppContext.BaseDirectory, "bingo-board.json");
        }

        var board = BoardConfig.LoadFromFile(configPath);
        IStrategy strategy = strategyName.ToLowerInvariant() switch
        {
            "greedy" => new GreedyStrategy(),
            "unlocker" => new UnlockerStrategy(),
            _ => new GroupedByActivityStrategy()
        };

        Console.WriteLine($"BingoSim: strategy={strategy.Name}, runs={runs}, threads={threads}, config={Path.GetFileName(configPath)}");

        var runTimes = new double[runs];
        var runPoints = new int[runs];
        RunResult? sample = null;

        // Determine a base seed to ensure distinct seeds across runs
        int baseSeed = seed ?? new Random().Next(int.MinValue, int.MaxValue);

        var po = new ParallelOptions { MaxDegreeOfParallelism = threads };
        Parallel.For(0, runs, po, i =>
        {
            // Clone the board per run to avoid shared mutable state
            var clonedBoard = board.DeepClone();
            // Derive a deterministic unique seed per run
            int runSeed = unchecked(baseSeed + i * 9973);
            var sim = new Simulator(clonedBoard, strategy, runSeed);
            var res = sim.Run();
            runTimes[i] = res.TotalTimeMinutes;
            runPoints[i] = res.TotalPoints;
            if (i == 0)
            {
                // It's okay if multiple threads race to write the same object reference; they compute the same run 0
                sample = res;
            }
        });

        double avgTime = runTimes.Average();
        double stdTime = Math.Sqrt(runTimes.Select(t => (t - avgTime) * (t - avgTime)).Average());
        double avgPoints = runPoints.Average();

        Console.WriteLine($"Avg total time: {avgTime:F1} min (std {stdTime:F1}), avg points: {avgPoints:F2}");

        if (sample != null)
        {
            Console.WriteLine("Sample run unlock times (row: minutes):");
            foreach (var kv in sample.RowUnlockTimesMinutes.OrderBy(k => k.Key))
            {
                Console.WriteLine($"  Row {kv.Key}: {kv.Value:F1}m");
            }
            Console.WriteLine("Sample run completion order:");
            foreach (var c in sample.CompletionOrder)
            {
                Console.WriteLine($"  t={c.CompletionTimeMinutes:F1}m: Row {c.RowIndex} - {c.TileId} (+{c.Points})");
            }
        }

        Console.WriteLine("Done.");
    }
}