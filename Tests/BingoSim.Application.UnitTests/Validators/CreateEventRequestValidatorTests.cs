using BingoSim.Application.DTOs;
using BingoSim.Application.Validators;
using FluentAssertions;

namespace BingoSim.Application.UnitTests.Validators;

public class CreateEventRequestValidatorTests
{
    private readonly CreateEventRequestValidator _validator = new();

    private static CreateEventRequest ValidRequest()
    {
        var activityId = Guid.NewGuid();
        var row = new RowDto(0, [
            new TileDto("r0.p1", "Tile 1", 1, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])]),
            new TileDto("r0.p2", "Tile 2", 2, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])]),
            new TileDto("r0.p3", "Tile 3", 3, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])]),
            new TileDto("r0.p4", "Tile 4", 4, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])])
        ]);
        return new CreateEventRequest("Test Event", TimeSpan.FromHours(24), 5, [row]);
    }

    [Fact]
    public async Task Validate_ValidRequest_Passes()
    {
        var request = ValidRequest();
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyName_Fails(string? name)
    {
        var request = ValidRequest() with { Name = name! };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_ZeroDuration_Fails()
    {
        var request = ValidRequest() with { Duration = TimeSpan.Zero };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Duration");
    }

    [Fact]
    public async Task Validate_NegativeUnlockPoints_Fails()
    {
        var request = ValidRequest() with { UnlockPointsRequiredPerRow = -1 };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UnlockPointsRequiredPerRow");
    }

    [Fact]
    public async Task Validate_RowWithOnlyThreeTiles_Fails()
    {
        var activityId = Guid.NewGuid();
        var row = new RowDto(0, [
            new TileDto("r0.p1", "Tile 1", 1, 1, [new TileActivityRuleDto(activityId, "key", null, [], [], [])]),
            new TileDto("r0.p2", "Tile 2", 2, 1, [new TileActivityRuleDto(activityId, "key", null, [], [], [])]),
            new TileDto("r0.p3", "Tile 3", 3, 1, [new TileActivityRuleDto(activityId, "key", null, [], [], [])])
        ]);
        var request = ValidRequest() with { Rows = [row] };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_DuplicateTileKeyAcrossEvent_Fails()
    {
        var activityId = Guid.NewGuid();
        var rule = new TileActivityRuleDto(activityId, "key", null, [], [], []);
        var row0 = new RowDto(0, [
            new TileDto("r0.p1", "T1", 1, 1, [rule]),
            new TileDto("r0.p2", "T2", 2, 1, [rule]),
            new TileDto("r0.p3", "T3", 3, 1, [rule]),
            new TileDto("r0.p4", "T4", 4, 1, [rule])
        ]);
        var row1 = new RowDto(1, [
            new TileDto("r0.p1", "T1", 1, 1, [rule]),
            new TileDto("r1.p2", "T2", 2, 1, [rule]),
            new TileDto("r1.p3", "T3", 3, 1, [rule]),
            new TileDto("r1.p4", "T4", 4, 1, [rule])
        ]);
        var request = ValidRequest() with { Rows = [row0, row1] };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }
}
