using Microsoft.AspNetCore.Components;
using BingoSim.Web.Components.Shared;
using Bunit;
using FluentAssertions;
using Xunit;

namespace BingoSim.Web.Tests.Components.Shared;

public class FormFieldTests : BunitContext
{
    [Fact]
    public void Renders_WithLabel_ShowsLabel()
    {
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Run count")
            .Add(p => p.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "input");
                builder.CloseElement();
            })));

        cut.Find(".form-field__label").TextContent.Trim().Should().Contain("Run count");
    }

    [Fact]
    public void Renders_WithHelperText_ShowsHelperText()
    {
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Run count")
            .Add(p => p.HelperText, "Number of simulation runs per team (1–100,000).")
            .Add(p => p.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "input");
                builder.CloseElement();
            })));

        cut.Find(".form-field__helper").TextContent.Trim().Should().Be("Number of simulation runs per team (1–100,000).");
    }

    [Fact]
    public void Renders_WithTooltip_ShowsTooltipIcon()
    {
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Execution mode")
            .Add(p => p.Tooltip, "Local: in-process. Distributed: worker processes.")
            .Add(p => p.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "div");
                builder.CloseElement();
            })));

        var tooltipSpan = cut.Find(".form-field__tooltip");
        tooltipSpan.GetAttribute("title").Should().Be("Local: in-process. Distributed: worker processes.");
    }

    [Fact]
    public void Renders_WithoutHelperText_DoesNotShowHelper()
    {
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Run count")
            .Add(p => p.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "input");
                builder.CloseElement();
            })));

        cut.FindAll(".form-field__helper").Should().BeEmpty();
    }
}
