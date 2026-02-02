using BingoSim.Application.DTOs;
using BingoSim.Application.StrategyKeys;
using BingoSim.Application.Validators;
using FluentAssertions;

namespace BingoSim.Application.UnitTests.Validators;

public class CreateTeamRequestValidatorTests
{
    private readonly CreateTeamRequestValidator _validator = new();

    private static CreateTeamRequest ValidRequest() =>
        new(Guid.NewGuid(), "Team Alpha", [], StrategyCatalog.RowRush, null);

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
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Validate_TeamMustBelongToEvent_FailsWhenEventIdDefault()
    {
        var request = ValidRequest() with { EventId = Guid.Empty };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EventId");
    }

    [Fact]
    public async Task Validate_NoDuplicatePlayerInTeam_FailsWhenDuplicatePlayerIds()
    {
        var playerId = Guid.NewGuid();
        var request = ValidRequest() with { PlayerProfileIds = [playerId, playerId] };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PlayerProfileIds" && e.ErrorMessage.Contains("once", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Validate_StrategyKeyMustBeSupported_FailsWhenInvalidKey()
    {
        var request = ValidRequest() with { StrategyKey = "InvalidStrategy" };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyKey");
    }

    [Theory]
    [InlineData(StrategyCatalog.RowRush)]
    [InlineData(StrategyCatalog.GreedyPoints)]
    public async Task Validate_SupportedStrategyKey_Passes(string key)
    {
        var request = ValidRequest() with { StrategyKey = key };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyPlayerList_Passes()
    {
        var request = ValidRequest() with { PlayerProfileIds = [] };
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }
}
