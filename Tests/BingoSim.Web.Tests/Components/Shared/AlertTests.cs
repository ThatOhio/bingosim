using BingoSim.Web.Components.Shared;
using Bunit;
using FluentAssertions;
using Xunit;

namespace BingoSim.Web.Tests.Components.Shared;

public class AlertTests : BunitContext
{
    [Fact]
    public void Renders_WithMessage_ShowsMessage()
    {
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Something went wrong"));

        cut.Find(".alert").TextContent.Trim().Should().Be("Something went wrong");
    }

    [Fact]
    public void Renders_WithNullMessage_ShowsNothing()
    {
        var cut = Render<Alert>();

        cut.FindAll(".alert").Should().BeEmpty();
    }

    [Fact]
    public void Renders_WithEmptyMessage_ShowsNothing()
    {
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, string.Empty));

        cut.FindAll(".alert").Should().BeEmpty();
    }

    [Theory]
    [InlineData(AlertSeverity.Error, "alert--error")]
    [InlineData(AlertSeverity.Warning, "alert--warning")]
    [InlineData(AlertSeverity.Info, "alert--info")]
    [InlineData(AlertSeverity.Success, "alert--success")]
    public void Renders_WithSeverity_AppliesCorrectClass(AlertSeverity severity, string expectedClass)
    {
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Test")
            .Add(p => p.Severity, severity));

        cut.Find(".alert").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void Renders_HasRoleAlert()
    {
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Error message"));

        cut.Find(".alert").GetAttribute("role").Should().Be("alert");
    }
}
