using System.Text.RegularExpressions;
using BingoSim.Worker.Consumers;
using Microsoft.Extensions.Options;

namespace BingoSim.Worker.Configuration;

/// <summary>
/// Post-configures WorkerSimulationOptions to derive WorkerIndex from HOSTNAME when not explicitly set.
/// Enables worker partitioning with Docker Compose replicas, where each container gets a unique
/// hostname like bingosim_bingosim.worker_1, bingosim_bingosim.worker_2, etc.
/// </summary>
public class WorkerIndexHostnameResolver : IPostConfigureOptions<WorkerSimulationOptions>
{
    /// <summary>
    /// Matches trailing replica number in Docker Compose hostnames: _1, _2, -1, -2, etc.
    /// </summary>
    private static readonly Regex HostnameReplicaPattern = new(@"[_-](\d+)$", RegexOptions.Compiled);

    public void PostConfigure(string? name, WorkerSimulationOptions options)
    {
        if (options.WorkerIndex.HasValue)
            return;

        var hostname = Environment.GetEnvironmentVariable("HOSTNAME") ?? "";
        var match = HostnameReplicaPattern.Match(hostname);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var replica) || replica < 1)
            return;

        var index = replica - 1;
        if (index < options.WorkerCount)
            options.WorkerIndex = index;
    }
}
