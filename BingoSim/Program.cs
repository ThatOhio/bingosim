using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;
using BingoSim.Config;
using BingoSim.Models;
using BingoSim.Simulation;
using BingoSim.Strategies;

namespace BingoSim;

class Program
{
    static void Main(string[] args)
    {
        // Args: --config <path> --runs <N> --strategy <greedy|grouped|unlocker|row-threshold|risk-averse|risk-seeking|ppm-row-bonus|row-sweep|monte-carlo|all> --seed <int> --threads <N> [--csv <path>]
        string configPath = Path.Combine(AppContext.BaseDirectory, "bingo-board.json");
        int runs = 1;
        string strategyName = "monte-carlo"; // default strategy
        int? seed = null;
        int threads = Environment.ProcessorCount;
        string? csvPath = $"/home/ohio/Documents/Temp/Bingo/{strategyName}{DateTime.Now.ToShortTimeString()}.csv";

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
                case "--csv":
                    if (i + 1 < args.Length) csvPath = args[++i];
                    break;
            }
        }

        if (!File.Exists(configPath))
        {
            Console.WriteLine($"Config not found at {configPath}. Using embedded sample.");
            configPath = Path.Combine(AppContext.BaseDirectory, "bingo-board.json");
        }

        // Load base board config
        var baseBoard = BoardConfig.LoadFromFile(configPath);

        // Strategy selection
        var normalized = strategyName.ToLowerInvariant();
        var strategies = normalized == "all" ? GetAllStrategies() : new List<IStrategy> { GetStrategyByName(normalized) };

        Console.WriteLine($"BingoSim: strategy={(normalized=="all"?"all":strategies[0].Name)}, runs={runs}, threads={threads}, config={Path.GetFileName(configPath)}");

        // Prepare CSV writer if requested
        StringBuilder? csv = null;
        if (!string.IsNullOrWhiteSpace(csvPath))
        {
            csv = new StringBuilder();
            csv.AppendLine("strategy,runIndex,totalTimeMinutes,totalPoints");
        }

        var aggregateSummaries = new List<(string name, double avg, double std)>();

        int baseSeed = seed ?? new Random().Next(int.MinValue, int.MaxValue);

        var totalSw = Stopwatch.StartNew();
        foreach (var strat in strategies)
        {
            var sw = Stopwatch.StartNew();
            // Run simulations for this strategy
            var (times, points, sample) = RunMany(baseBoard, strat, runs, threads, baseSeed);
            sw.Stop();

            double avgTime = times.Average();
            // Standard deviation of total run times across all simulations
            double stdTime = Math.Sqrt(times.Select(t => (t - avgTime) * (t - avgTime)).Average());
            aggregateSummaries.Add((strat.Name, avgTime, stdTime));

            Console.WriteLine($"Strategy {strat.Name}: Avg total time {avgTime:F1} min (std {stdTime:F1}) | compute {sw.Elapsed.TotalSeconds:F2}s");

            // Append CSV rows if requested
            if (csv != null)
            {
                for (int i = 0; i < runs; i++)
                {
                    csv.Append(strat.Name).Append(',')
                       .Append(i.ToString(CultureInfo.InvariantCulture)).Append(',')
                       .Append(times[i].ToString(CultureInfo.InvariantCulture)).Append(',')
                       .Append(points[i].ToString(CultureInfo.InvariantCulture)).AppendLine();
                }
            }

            // For the first strategy (or single run), print a sample breakdown
            if (sample != null && strategies.Count == 1)
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
        }
        totalSw.Stop();

        // Persist CSV if requested
        if (csv != null && !string.IsNullOrWhiteSpace(csvPath))
        {
            try
            {
                File.WriteAllText(csvPath!, csv.ToString());
                Console.WriteLine($"Wrote CSV to {csvPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write CSV to {csvPath}: {ex.Message}");
            }
        }

        // If multiple strategies, print a compact comparison summary at the end
        if (strategies.Count > 1)
        {
            Console.WriteLine("\nComparison (sorted by avg total time):");
            foreach (var row in aggregateSummaries.OrderBy(r => r.avg))
            {
                Console.WriteLine($"  {row.name}: avg={row.avg:F1}m (std {row.std:F1})");
            }
            Console.WriteLine($"Total compute time for all strategies: {totalSw.Elapsed.TotalSeconds:F2}s");
        }
        else
        {
            Console.WriteLine($"Total compute time: {totalSw.Elapsed.TotalSeconds:F2}s");
        }

        Console.WriteLine("Done.");
    }

    private static (double[] times, int[] points, RunResult? sample) RunMany(Board baseBoard, IStrategy strategy, int runs, int threads, int baseSeed)
    {
        var runTimes = new double[runs];
        var runPoints = new int[runs];
        RunResult? sample = null;
        var po = new ParallelOptions { MaxDegreeOfParallelism = threads };

        Parallel.For(0, runs, po, i =>
        {
            var clonedBoard = baseBoard.DeepClone();
            int runSeed = unchecked(baseSeed + i * 9973 + strategy.Name.GetHashCode());
            var sim = new Simulator(clonedBoard, strategy, runSeed);
            var res = sim.Run();
            runTimes[i] = res.TotalTimeMinutes;
            runPoints[i] = res.TotalPoints;
            if (i == 0)
            {
                sample = res;
            }
        });

        return (runTimes, runPoints, sample);
    }

    private static List<IStrategy> GetAllStrategies() => new()
    {
        new GreedyStrategy(),
        new GroupedByActivityStrategy(),
        new UnlockerStrategy(),
        new RowThresholdStrategy(),
        new RiskAverseStrategy(),
        new RiskSeekingStrategy(),
        new RowWeightedBonusStrategy(),
        new CompletionistRowSweepStrategy(),
        new MonteCarloLookaheadStrategy()
    };

    private static IStrategy GetStrategyByName(string name) => name switch
    {
        "greedy" => new GreedyStrategy(),
        "grouped" => new GroupedByActivityStrategy(),
        "unlocker" => new UnlockerStrategy(),
        "row-threshold" => new RowThresholdStrategy(),
        "risk-averse" => new RiskAverseStrategy(),
        "risk-seeking" => new RiskSeekingStrategy(),
        "row-sweep" => new CompletionistRowSweepStrategy(),
        "completionist" => new CompletionistRowSweepStrategy(),
        "monte-carlo" => new MonteCarloLookaheadStrategy(),
        _ => new GroupedByActivityStrategy()
    };
}