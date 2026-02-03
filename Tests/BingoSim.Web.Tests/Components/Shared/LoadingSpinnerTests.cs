using BingoSim.Web.Components.Shared;
using Bunit;
using FluentAssertions;
using Xunit;

namespace BingoSim.Web.Tests.Components.Shared;

public class LoadingSpinnerTests : BunitContext
{
    [Fact]
    public void Renders_WithNoLabel_ShowsSpinnerOnly()
    {
        var cut = Render<LoadingSpinner>();

        cut.Find(".loading-spinner__dot").Should().NotBeNull();
        cut.FindAll(".loading-spinner__label").Should().BeEmpty();
    }

    [Fact]
    public void Renders_WithLabel_ShowsSpinnerAndLabel()
    {
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Label, "Loading events…"));

        cut.Find(".loading-spinner__dot").Should().NotBeNull();
        cut.Find(".loading-spinner__label").TextContent.Should().Be("Loading events…");
    }

    [Fact]
    public void Renders_Inline_AddsInlineClass()
    {
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Inline, true));

        cut.Find(".loading-spinner").ClassList.Should().Contain("loading-spinner--inline");
    }

    [Fact]
    public void Renders_Block_AddsBlockClass()
    {
        var cut = Render<LoadingSpinner>();

        cut.Find(".loading-spinner").ClassList.Should().Contain("loading-spinner--block");
    }

    [Fact]
    public void Renders_HasAriaBusy()
    {
        var cut = Render<LoadingSpinner>();

        cut.Find(".loading-spinner").GetAttribute("aria-busy").Should().Be("true");
    }
}
