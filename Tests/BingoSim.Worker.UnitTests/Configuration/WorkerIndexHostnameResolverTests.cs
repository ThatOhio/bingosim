using BingoSim.Worker.Configuration;
using BingoSim.Worker.Consumers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BingoSim.Worker.UnitTests.Configuration;

public class WorkerIndexHostnameResolverTests
{
    [Fact]
    public void PostConfigure_WorkerIndexAlreadySet_DoesNotOverride()
    {
        var options = new WorkerSimulationOptions { WorkerIndex = 2, WorkerCount = 3 };
        var resolver = new WorkerIndexHostnameResolver();

        resolver.PostConfigure(null, options);

        options.WorkerIndex.Should().Be(2);
    }

    [Theory]
    [InlineData("bingosim_bingosim.worker_1", 0)]
    [InlineData("bingosim_bingosim.worker_2", 1)]
    [InlineData("bingosim_bingosim.worker_3", 2)]
    [InlineData("myproject_myservice-1", 0)]
    [InlineData("myproject_myservice-2", 1)]
    public void PostConfigure_HostnameWithTrailingNumber_SetsWorkerIndex(string hostname, int expectedIndex)
    {
        Environment.SetEnvironmentVariable("HOSTNAME", hostname);
        try
        {
            var options = new WorkerSimulationOptions { WorkerCount = 3 };
            var resolver = new WorkerIndexHostnameResolver();

            resolver.PostConfigure(null, options);

            options.WorkerIndex.Should().Be(expectedIndex);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOSTNAME", null);
        }
    }

    [Fact]
    public void PostConfigure_HostnameWithoutTrailingNumber_LeavesWorkerIndexNull()
    {
        Environment.SetEnvironmentVariable("HOSTNAME", "mycontainer");
        try
        {
            var options = new WorkerSimulationOptions { WorkerCount = 3 };
            var resolver = new WorkerIndexHostnameResolver();

            resolver.PostConfigure(null, options);

            options.WorkerIndex.Should().BeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOSTNAME", null);
        }
    }

    [Fact]
    public void PostConfigure_DerivedIndexExceedsWorkerCount_LeavesWorkerIndexNull()
    {
        Environment.SetEnvironmentVariable("HOSTNAME", "bingosim_worker_5");
        try
        {
            var options = new WorkerSimulationOptions { WorkerCount = 3 };
            var resolver = new WorkerIndexHostnameResolver();

            resolver.PostConfigure(null, options);

            options.WorkerIndex.Should().BeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOSTNAME", null);
        }
    }
}
