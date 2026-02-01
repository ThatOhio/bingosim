using BingoSim.Application.DTOs;
using BingoSim.Application.Validators;
using FluentAssertions;

namespace BingoSim.Application.UnitTests.Validators;

public class CreatePlayerProfileRequestValidatorTests
{
    private readonly CreatePlayerProfileRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Arrange
        var request = new CreatePlayerProfileRequest(
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
        var request = new CreatePlayerProfileRequest(
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
        var request = new CreatePlayerProfileRequest(
            Name: new string('a', 101),
            SkillTimeMultiplier: 1.0m,
            Capabilities: [],
            WeeklySchedule: new WeeklyScheduleDto([])
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage.Contains("100"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public void Validate_InvalidSkillMultiplier_ReturnsError(decimal multiplier)
    {
        // Arrange
        var request = new CreatePlayerProfileRequest(
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
    public void Validate_SkillMultiplierTooHigh_ReturnsError()
    {
        // Arrange
        var request = new CreatePlayerProfileRequest(
            Name: "Valid Name",
            SkillTimeMultiplier: 11m,
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
    public void Validate_NullCapabilities_ReturnsError()
    {
        // Arrange
        var request = new CreatePlayerProfileRequest(
            Name: "Valid Name",
            SkillTimeMultiplier: 1.0m,
            Capabilities: null!,
            WeeklySchedule: new WeeklyScheduleDto([])
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Capabilities");
    }

    [Fact]
    public void Validate_InvalidCapability_ReturnsError()
    {
        // Arrange
        var request = new CreatePlayerProfileRequest(
            Name: "Valid Name",
            SkillTimeMultiplier: 1.0m,
            Capabilities: [new CapabilityDto("", "Valid Name")],
            WeeklySchedule: new WeeklyScheduleDto([])
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Capabilities"));
    }

    [Fact]
    public void Validate_NullWeeklySchedule_ReturnsError()
    {
        // Arrange
        var request = new CreatePlayerProfileRequest(
            Name: "Valid Name",
            SkillTimeMultiplier: 1.0m,
            Capabilities: [],
            WeeklySchedule: null!
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "WeeklySchedule");
    }

    [Fact]
    public void Validate_InvalidSession_ReturnsError()
    {
        // Arrange
        var request = new CreatePlayerProfileRequest(
            Name: "Valid Name",
            SkillTimeMultiplier: 1.0m,
            Capabilities: [],
            WeeklySchedule: new WeeklyScheduleDto([
                new ScheduledSessionDto(DayOfWeek.Monday, new TimeOnly(18, 0), 0) // Invalid duration
            ])
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Sessions"));
    }

    [Fact]
    public void Validate_ValidRequestWithAllData_Passes()
    {
        // Arrange
        var request = new CreatePlayerProfileRequest(
            Name: "Complete Player",
            SkillTimeMultiplier: 0.8m,
            Capabilities: [
                new CapabilityDto("quest.ds2", "Desert Treasure 2"),
                new CapabilityDto("item.lance", "Dragon Hunter Lance")
            ],
            WeeklySchedule: new WeeklyScheduleDto([
                new ScheduledSessionDto(DayOfWeek.Monday, new TimeOnly(18, 0), 120),
                new ScheduledSessionDto(DayOfWeek.Wednesday, new TimeOnly(19, 0), 90)
            ])
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
