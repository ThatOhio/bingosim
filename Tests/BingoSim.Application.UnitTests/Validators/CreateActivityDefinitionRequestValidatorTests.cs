using BingoSim.Application.DTOs;
using BingoSim.Application.Validators;
using FluentAssertions;

namespace BingoSim.Application.UnitTests.Validators;

public class CreateActivityDefinitionRequestValidatorTests
{
    private readonly CreateActivityDefinitionRequestValidator _validator = new();

    private static CreateActivityDefinitionRequest ValidRequest() =>
        new(
            Key: "activity.zulrah",
            Name: "Zulrah",
            ModeSupport: new ActivityModeSupportDto(true, true, 1, 8),
            Attempts: [
                new ActivityAttemptDefinitionDto(
                    "personal_loot",
                    0,
                    new AttemptTimeModelDto(60, 0, 10),
                    [
                        new ActivityOutcomeDefinitionDto("common", 1, 2, [new ProgressGrantDto("drop.common", 1)]),
                        new ActivityOutcomeDefinitionDto("rare", 1, 100, [new ProgressGrantDto("drop.rare", 3)])
                    ])
            ],
            GroupScalingBands: [new GroupSizeBandDto(1, 1, 1.0m, 1.0m), new GroupSizeBandDto(2, 4, 0.9m, 1.1m)]);

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var result = _validator.Validate(ValidRequest());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyKey_ReturnsError(string? key)
    {
        var request = ValidRequest() with { Key = key! };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyName_ReturnsError(string? name)
    {
        var request = ValidRequest() with { Name = name! };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_EmptyAttempts_ReturnsError()
    {
        var request = ValidRequest() with { Attempts = [] };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Attempts");
    }

    [Fact]
    public void Validate_AttemptWithEmptyOutcomes_ReturnsError()
    {
        var request = ValidRequest() with
        {
            Attempts = [
                new ActivityAttemptDefinitionDto(
                    "attempt_1",
                    0,
                    new AttemptTimeModelDto(60, 0, null),
                    [])
            ]
        };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ProgressGrantWithZeroUnits_ReturnsError()
    {
        var request = ValidRequest() with
        {
            Attempts = [
                new ActivityAttemptDefinitionDto(
                    "attempt_1",
                    0,
                    new AttemptTimeModelDto(60, 0, null),
                    [new ActivityOutcomeDefinitionDto("outcome_1", 1, 1, [new ProgressGrantDto("drop.key", 0)])])
            ]
        };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_GroupSizeBandMaxLessThanMin_ReturnsError()
    {
        var request = ValidRequest() with
        {
            GroupScalingBands = [new GroupSizeBandDto(5, 3, 1.0m, 1.0m)]
        };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
    }
}
