using BingoSim.Application.DTOs;
using BingoSim.Application.Validators;
using FluentAssertions;

namespace BingoSim.Application.UnitTests.Validators;

public class UpdateEventRequestValidatorTests
{
    private readonly UpdateEventRequestValidator _validator = new();

    private static UpdateEventRequest ValidRequest()
    {
        var activityId = Guid.NewGuid();
        var row = new RowDto(0, [
            new TileDto("r0.p1", "Tile 1", 1, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])]),
            new TileDto("r0.p2", "Tile 2", 2, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])]),
            new TileDto("r0.p3", "Tile 3", 3, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])]),
            new TileDto("r0.p4", "Tile 4", 4, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])])
        ]);
        return new UpdateEventRequest("Test Event", TimeSpan.FromHours(24), 5, [row]);
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
    public async Task Validate_EmptyName_Fails(string? name)
    {
        var request = ValidRequest() with { Name = name! };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
    }
}
