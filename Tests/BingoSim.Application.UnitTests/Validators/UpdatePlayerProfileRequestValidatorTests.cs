using BingoSim.Application.DTOs;
using BingoSim.Application.Validators;
using FluentAssertions;

namespace BingoSim.Application.UnitTests.Validators;

public class UpdatePlayerProfileRequestValidatorTests
{
    private readonly UpdatePlayerProfileRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Arrange
        var request = new UpdatePlayerProfileRequest(
            Name: "Valid Player",
            SkillTimeMultiplier: 1.0m,
            Capabilities: [],
            WeeklySchedule: new WeeklyScheduleDto([])
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyName_ReturnsError(string? name)
    {
        // Arrange
        var request = new UpdatePlayerProfileRequest(
            Name: name!,
            SkillTimeMultiplier: 1.0m,
            Capabilities: [],
            WeeklySchedule: new WeeklyScheduleDto([])
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_NameTooLong_ReturnsError()
    {
        // Arrange
        var request = new UpdatePlayerProfileRequest(
            Name: new string('a', 101),
            SkillTimeMultiplier: 1.0m,
            Capabilities: [],
            WeeklySchedule: new WeeklyScheduleDto([])
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidSkillMultiplier_ReturnsError(decimal multiplier)
    {
        // Arrange
        var request = new UpdatePlayerProfileRequest(
            Name: "Valid Name",
            SkillTimeMultiplier: multiplier,
            Capabilities: [],
            WeeklySchedule: new WeeklyScheduleDto([])
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SkillTimeMultiplier");
    }

    [Fact]
    public void Validate_ValidRequestWithCompleteData_Passes()
    {
        // Arrange
        var request = new UpdatePlayerProfileRequest(
            Name: "Updated Player",
            SkillTimeMultiplier: 1.2m,
            Capabilities: [
                new CapabilityDto("quest.sote", "Song of the Elves")
            ],
            WeeklySchedule: new WeeklyScheduleDto([
                new ScheduledSessionDto(DayOfWeek.Saturday, new TimeOnly(10, 0), 240)
            ])
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
