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

    [Fact]
    public void VariableConstructor_ValidRange_CreatesVariableGrant()
    {
        var vo = new ProgressGrant("item.arrows", 50, 100);
        vo.DropKey.Should().Be("item.arrows");
        vo.UnitsMin.Should().Be(50);
        vo.UnitsMax.Should().Be(100);
        vo.IsVariable.Should().BeTrue();
    }

    [Fact]
    public void VariableConstructor_SingleValue_CreatesValidGrant()
    {
        var vo = new ProgressGrant("drop.key", 75, 75);
        vo.UnitsMin.Should().Be(75);
        vo.UnitsMax.Should().Be(75);
        vo.IsVariable.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 50)]
    public void VariableConstructor_UnitsMinLessThanOne_Throws(int min, int max)
    {
        var act = () => new ProgressGrant("drop.key", min, max);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("unitsMin");
    }

    [Theory]
    [InlineData(50, 0)]
    [InlineData(100, -1)]
    public void VariableConstructor_UnitsMaxLessThanOne_Throws(int min, int max)
    {
        var act = () => new ProgressGrant("drop.key", min, max);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("unitsMax");
    }

    [Fact]
    public void VariableConstructor_MinGreaterThanMax_Throws()
    {
        var act = () => new ProgressGrant("drop.key", 100, 50);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("unitsMax");
    }
}
