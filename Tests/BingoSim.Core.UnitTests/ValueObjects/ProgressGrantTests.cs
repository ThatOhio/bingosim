using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class ProgressGrantTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesValueObject()
    {
        var vo = new ProgressGrant("drop.magic_fang", 3);
        vo.DropKey.Should().Be("drop.magic_fang");
        vo.Units.Should().Be(3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyDropKey_Throws(string? dropKey)
    {
        var act = () => new ProgressGrant(dropKey!, 1);
        act.Should().Throw<ArgumentException>().WithParameterName("dropKey");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_UnitsLessThanOne_Throws(int units)
    {
        var act = () => new ProgressGrant("drop.key", units);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("units");
    }
}
