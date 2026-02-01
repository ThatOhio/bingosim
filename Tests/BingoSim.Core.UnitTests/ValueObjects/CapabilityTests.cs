using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class CapabilityTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesCapability()
    {
        // Arrange
        var key = "quest.ds2";
        var name = "Desert Treasure 2";

        // Act
        var capability = new Capability(key, name);

        // Assert
        capability.Key.Should().Be(key);
        capability.Name.Should().Be(name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyKey_ThrowsArgumentException(string? key)
    {
        // Arrange & Act
        var act = () => new Capability(key!, "Valid Name");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyName_ThrowsArgumentException(string? name)
    {
        // Arrange & Act
        var act = () => new Capability("valid.key", name!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var cap1 = new Capability("quest.ds2", "Desert Treasure 2");
        var cap2 = new Capability("quest.ds2", "Desert Treasure 2");

        // Assert
        cap1.Should().Be(cap2);
    }

    [Fact]
    public void Equality_DifferentKeys_AreNotEqual()
    {
        // Arrange
        var cap1 = new Capability("quest.ds2", "Desert Treasure 2");
        var cap2 = new Capability("quest.dt1", "Desert Treasure 2");

        // Assert
        cap1.Should().NotBe(cap2);
    }
}
