using BingoSim.Application.DTOs;
using BingoSim.Application.StrategyKeys;
using BingoSim.Application.Validators;
using FluentAssertions;

namespace BingoSim.Application.UnitTests.Validators;

public class UpdateTeamRequestValidatorTests
{
    private readonly UpdateTeamRequestValidator _validator = new();

    private static UpdateTeamRequest ValidRequest() =>
        new("Team Alpha", [], StrategyCatalog.RowRush, null);

    [Fact]
    public async Task Validate_ValidRequest_Passes()
    {
        var request = ValidRequest();
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_TeamNameRequired_FailsWhenEmpty()
    {
        var request = ValidRequest() with { Name = "" };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_NoDuplicatePlayerInTeam_FailsWhenDuplicatePlayerIds()
    {
        var playerId = Guid.NewGuid();
        var request = ValidRequest() with { PlayerProfileIds = [playerId, playerId] };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PlayerProfileIds");
    }

    [Fact]
    public async Task Validate_StrategyKeyMustBeSupported_FailsWhenInvalidKey()
    {
        var request = ValidRequest() with { StrategyKey = "InvalidStrategy" };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyKey");
    }
}
