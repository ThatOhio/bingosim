using Microsoft.AspNetCore.Components;
using BingoSim.Web.Components.Shared;
using Bunit;
using FluentAssertions;
using Xunit;

namespace BingoSim.Web.Tests.Components.Shared;

public class RefreshIndicatorTests : BunitContext
{
    [Fact]
    public void Renders_WithNullLastUpdated_ShowsPlaceholder()
    {
        var cut = Render<RefreshIndicator>();

        cut.Find(".refresh-indicator__last-updated").TextContent.Should().Contain("—");
    }

    [Fact]
    public void Renders_WithLastUpdated_ShowsFormattedTimestamp()
    {
        var when = new DateTimeOffset(2025, 2, 3, 14, 30, 0, TimeSpan.Zero);
        var cut = Render<RefreshIndicator>(parameters => parameters
            .Add(p => p.LastUpdated, when));

        var text = cut.Find(".refresh-indicator__last-updated").TextContent;
        text.Should().Contain("2025");
        text.Should().NotContain("—");
    }

    [Fact]
    public void Renders_WithIntervalSeconds_ShowsInterval()
    {
        var cut = Render<RefreshIndicator>(parameters => parameters
            .Add(p => p.IntervalSeconds, 5));

        cut.Find(".refresh-indicator__interval").TextContent.Should().Contain("every 5 s");
    }

    [Fact]
    public void RefreshButton_Click_InvokesOnRefresh()
    {
        var invoked = false;
        var cut = Render<RefreshIndicator>(parameters => parameters
            .Add(p => p.OnRefresh, EventCallback.Factory.Create(this, () => invoked = true)));

        cut.FindAll("button").First(b => b.TextContent.Contains("Refresh")).Click();

        invoked.Should().BeTrue();
    }

    [Fact]
    public void PauseButton_Click_InvokesOnPauseToggle()
    {
        var invoked = false;
        var cut = Render<RefreshIndicator>(parameters => parameters
            .Add(p => p.IsPaused, false)
            .Add(p => p.OnPauseToggle, EventCallback.Factory.Create(this, () => invoked = true)));

        cut.FindAll("button").First(b => b.TextContent.Contains("Pause")).Click();

        invoked.Should().BeTrue();
    }

    [Fact]
    public void WhenPaused_ShowsResumeButton()
    {
        var cut = Render<RefreshIndicator>(parameters => parameters
            .Add(p => p.IsPaused, true));

        cut.FindAll("button").First(b => b.TextContent.Contains("Resume")).Should().NotBeNull();
    }

    [Fact]
    public void WhenResumed_ShowsPauseButton()
    {
        var cut = Render<RefreshIndicator>(parameters => parameters
            .Add(p => p.IsPaused, false));

        cut.FindAll("button").First(b => b.TextContent.Contains("Pause")).Should().NotBeNull();
    }

    [Fact]
    public void WhenIsRefreshing_RefreshButtonIsDisabled()
    {
        var cut = Render<RefreshIndicator>(parameters => parameters
            .Add(p => p.IsRefreshing, true));

        var refreshButton = cut.FindAll("button").First(b => b.TextContent.Contains("Refreshing"));
        refreshButton.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void WhenIsRefreshing_ShowsRefreshingText()
    {
        var cut = Render<RefreshIndicator>(parameters => parameters
            .Add(p => p.IsRefreshing, true));

        cut.Markup.Should().Contain("Refreshing…");
    }
}
