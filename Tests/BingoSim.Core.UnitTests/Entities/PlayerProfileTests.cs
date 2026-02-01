using BingoSim.Core.Entities;
using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.Entities;

public class PlayerProfileTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesPlayerProfile()
    {
        // Arrange
        var name = "TestPlayer";
        var skillMultiplier = 1.0m;

        // Act
        var profile = new PlayerProfile(name, skillMultiplier);

        // Assert
        profile.Id.Should().NotBe(Guid.Empty);
        profile.Name.Should().Be(name);
        profile.SkillTimeMultiplier.Should().Be(skillMultiplier);
        profile.Capabilities.Should().BeEmpty();
        profile.WeeklySchedule.Should().NotBeNull();
        profile.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_DefaultSkillMultiplier_UsesOne()
    {
        // Act
        var profile = new PlayerProfile("TestPlayer");

        // Assert
        profile.SkillTimeMultiplier.Should().Be(1.0m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyName_ThrowsArgumentException(string? name)
    {
        // Act
        var act = () => new PlayerProfile(name!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public void Constructor_InvalidSkillMultiplier_ThrowsArgumentOutOfRangeException(decimal multiplier)
    {
        // Act
        var act = () => new PlayerProfile("TestPlayer", multiplier);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("skillTimeMultiplier");
    }

    [Fact]
    public void UpdateName_ValidName_UpdatesName()
    {
        // Arrange
        var profile = new PlayerProfile("OldName");

        // Act
        profile.UpdateName("NewName");

        // Assert
        profile.Name.Should().Be("NewName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateName_EmptyName_ThrowsArgumentException(string? name)
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");

        // Act
        var act = () => profile.UpdateName(name!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void UpdateSkillTimeMultiplier_ValidValue_UpdatesMultiplier()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");

        // Act
        profile.UpdateSkillTimeMultiplier(0.8m);

        // Assert
        profile.SkillTimeMultiplier.Should().Be(0.8m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateSkillTimeMultiplier_InvalidValue_ThrowsArgumentOutOfRangeException(decimal multiplier)
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");

        // Act
        var act = () => profile.UpdateSkillTimeMultiplier(multiplier);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("multiplier");
    }

    [Fact]
    public void AddCapability_ValidCapability_AddsToList()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");
        var capability = new Capability("quest.ds2", "Desert Treasure 2");

        // Act
        profile.AddCapability(capability);

        // Assert
        profile.Capabilities.Should().ContainSingle()
            .Which.Should().Be(capability);
    }

    [Fact]
    public void AddCapability_DuplicateKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");
        var capability = new Capability("quest.ds2", "Desert Treasure 2");
        profile.AddCapability(capability);

        // Act
        var act = () => profile.AddCapability(new Capability("quest.ds2", "DT2"));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*quest.ds2*");
    }

    [Fact]
    public void AddCapability_NullCapability_ThrowsArgumentNullException()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");

        // Act
        var act = () => profile.AddCapability(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveCapability_ExistingKey_RemovesCapability()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");
        profile.AddCapability(new Capability("quest.ds2", "Desert Treasure 2"));

        // Act
        profile.RemoveCapability("quest.ds2");

        // Assert
        profile.Capabilities.Should().BeEmpty();
    }

    [Fact]
    public void RemoveCapability_NonExistingKey_DoesNothing()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");
        profile.AddCapability(new Capability("quest.ds2", "Desert Treasure 2"));

        // Act
        profile.RemoveCapability("quest.other");

        // Assert
        profile.Capabilities.Should().HaveCount(1);
    }

    [Fact]
    public void ClearCapabilities_WithCapabilities_RemovesAll()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");
        profile.AddCapability(new Capability("quest.ds2", "Desert Treasure 2"));
        profile.AddCapability(new Capability("item.lance", "Dragon Hunter Lance"));

        // Act
        profile.ClearCapabilities();

        // Assert
        profile.Capabilities.Should().BeEmpty();
    }

    [Fact]
    public void SetCapabilities_ReplacesAllCapabilities()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");
        profile.AddCapability(new Capability("old.cap", "Old Capability"));

        var newCapabilities = new[]
        {
            new Capability("new.cap1", "New Capability 1"),
            new Capability("new.cap2", "New Capability 2")
        };

        // Act
        profile.SetCapabilities(newCapabilities);

        // Assert
        profile.Capabilities.Should().HaveCount(2);
        profile.HasCapability("old.cap").Should().BeFalse();
        profile.HasCapability("new.cap1").Should().BeTrue();
        profile.HasCapability("new.cap2").Should().BeTrue();
    }

    [Fact]
    public void HasCapability_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");
        profile.AddCapability(new Capability("quest.ds2", "Desert Treasure 2"));

        // Act
        var result = profile.HasCapability("quest.ds2");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasCapability_NonExistingKey_ReturnsFalse()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");

        // Act
        var result = profile.HasCapability("quest.ds2");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SetWeeklySchedule_ValidSchedule_UpdatesSchedule()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");
        var schedule = new WeeklySchedule([
            new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120)
        ]);

        // Act
        profile.SetWeeklySchedule(schedule);

        // Assert
        profile.WeeklySchedule.Should().Be(schedule);
        profile.WeeklySchedule.Sessions.Should().HaveCount(1);
    }

    [Fact]
    public void SetWeeklySchedule_NullSchedule_ThrowsArgumentNullException()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer");

        // Act
        var act = () => profile.SetWeeklySchedule(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
