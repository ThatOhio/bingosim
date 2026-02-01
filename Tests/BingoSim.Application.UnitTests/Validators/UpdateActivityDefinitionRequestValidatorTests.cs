using BingoSim.Application.DTOs;
using BingoSim.Application.Validators;
using FluentAssertions;

namespace BingoSim.Application.UnitTests.Validators;

public class UpdateActivityDefinitionRequestValidatorTests
{
    private readonly UpdateActivityDefinitionRequestValidator _validator = new();

    private static UpdateActivityDefinitionRequest ValidRequest() =>
        new(
            Key: "activity.zulrah",
            Name: "Zulrah",
            ModeSupport: new ActivityModeSupportDto(true, true, 1, 8),
            Attempts: [
                new ActivityAttemptDefinitionDto(
                    "personal_loot",
                    0,
                    new AttemptTimeModelDto(60, 0, 10),
                    [new ActivityOutcomeDefinitionDto("common", 1, 2, [new ProgressGrantDto("drop.common", 1)])])
            ],
            GroupScalingBands: []);

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var result = _validator.Validate(ValidRequest());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyKey_ReturnsError()
    {
        var request = ValidRequest() with { Key = "" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Key");
    }

    [Fact]
    public void Validate_EmptyName_ReturnsError()
    {
        var request = ValidRequest() with { Name = "" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }
}
