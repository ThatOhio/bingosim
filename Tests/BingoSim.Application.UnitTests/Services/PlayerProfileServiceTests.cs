using BingoSim.Application.DTOs;
using BingoSim.Application.Services;
using BingoSim.Core.Entities;
using BingoSim.Core.Exceptions;
using BingoSim.Core.Interfaces;
using BingoSim.Core.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace BingoSim.Application.UnitTests.Services;

public class PlayerProfileServiceTests
{
    private readonly IPlayerProfileRepository _repository;
    private readonly ILogger<PlayerProfileService> _logger;
    private readonly PlayerProfileService _service;

    public PlayerProfileServiceTests()
    {
        _repository = Substitute.For<IPlayerProfileRepository>();
        _logger = Substitute.For<ILogger<PlayerProfileService>>();
        _service = new PlayerProfileService(_repository, _logger);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProfiles()
    {
        // Arrange
        var profiles = new List<PlayerProfile>
        {
            CreateTestProfile("Player1"),
            CreateTestProfile("Player2")
        };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(profiles);

        // Act
        var result = await _service.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Player1");
        result[1].Name.Should().Be("Player2");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsProfile()
    {
        // Arrange
        var profile = CreateTestProfile("TestPlayer");
        _repository.GetByIdAsync(profile.Id, Arg.Any<CancellationToken>()).Returns(profile);

        // Act
        var result = await _service.GetByIdAsync(profile.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("TestPlayer");
        result.Id.Should().Be(profile.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((PlayerProfile?)null);

        // Act
        var result = await _service.GetByIdAsync(id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesAndReturnsId()
    {
        // Arrange
        var request = CreateTestRequest("NewPlayer");

        // Act
        var id = await _service.CreateAsync(request);

        // Assert
        id.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AddAsync(
            Arg.Is<PlayerProfile>(p => p.Name == "NewPlayer"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithCapabilities_MapsCapabilitiesCorrectly()
    {
        // Arrange
        var request = new CreatePlayerProfileRequest(
            Name: "PlayerWithCaps",
            SkillTimeMultiplier: 1.0m,
            Capabilities: [
                new CapabilityDto("quest.ds2", "Desert Treasure 2"),
                new CapabilityDto("item.lance", "Dragon Hunter Lance")
            ],
            WeeklySchedule: new WeeklyScheduleDto([])
        );

        PlayerProfile? capturedProfile = null;
        await _repository.AddAsync(
            Arg.Do<PlayerProfile>(p => capturedProfile = p),
            Arg.Any<CancellationToken>());

        // Act
        await _service.CreateAsync(request);

        // Assert
        capturedProfile.Should().NotBeNull();
        capturedProfile!.Capabilities.Should().HaveCount(2);
        capturedProfile.HasCapability("quest.ds2").Should().BeTrue();
        capturedProfile.HasCapability("item.lance").Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithSchedule_MapsScheduleCorrectly()
    {
        // Arrange
        var request = new CreatePlayerProfileRequest(
            Name: "PlayerWithSchedule",
            SkillTimeMultiplier: 1.0m,
            Capabilities: [],
            WeeklySchedule: new WeeklyScheduleDto([
                new ScheduledSessionDto(DayOfWeek.Monday, new TimeOnly(18, 0), 120),
                new ScheduledSessionDto(DayOfWeek.Wednesday, new TimeOnly(19, 0), 90)
            ])
        );

        PlayerProfile? capturedProfile = null;
        await _repository.AddAsync(
            Arg.Do<PlayerProfile>(p => capturedProfile = p),
            Arg.Any<CancellationToken>());

        // Act
        await _service.CreateAsync(request);

        // Assert
        capturedProfile.Should().NotBeNull();
        capturedProfile!.WeeklySchedule.Sessions.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_ExistingProfile_UpdatesProfile()
    {
        // Arrange
        var profile = CreateTestProfile("OldName");
        _repository.GetByIdAsync(profile.Id, Arg.Any<CancellationToken>()).Returns(profile);

        var request = new UpdatePlayerProfileRequest(
            Name: "NewName",
            SkillTimeMultiplier: 0.8m,
            Capabilities: [],
            WeeklySchedule: new WeeklyScheduleDto([])
        );

        // Act
        await _service.UpdateAsync(profile.Id, request);

        // Assert
        await _repository.Received(1).UpdateAsync(
            Arg.Is<PlayerProfile>(p => p.Name == "NewName" && p.SkillTimeMultiplier == 0.8m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NonExistingProfile_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((PlayerProfile?)null);

        var request = CreateUpdateRequest("NewName");

        // Act
        var act = async () => await _service.UpdateAsync(id, request);

        // Assert
        await act.Should().ThrowAsync<PlayerProfileNotFoundException>()
            .Where(e => e.PlayerProfileId == id);
    }

    [Fact]
    public async Task DeleteAsync_ExistingProfile_DeletesProfile()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _service.DeleteAsync(id);

        // Assert
        await _repository.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NonExistingProfile_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var act = async () => await _service.DeleteAsync(id);

        // Assert
        await act.Should().ThrowAsync<PlayerProfileNotFoundException>()
            .Where(e => e.PlayerProfileId == id);
    }

    private static PlayerProfile CreateTestProfile(string name)
    {
        var profile = new PlayerProfile(name, 1.0m);
        profile.AddCapability(new Capability("test.cap", "Test Capability"));
        profile.SetWeeklySchedule(new WeeklySchedule([
            new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120)
        ]));
        return profile;
    }

    private static CreatePlayerProfileRequest CreateTestRequest(string name)
    {
        return new CreatePlayerProfileRequest(
            Name: name,
            SkillTimeMultiplier: 1.0m,
            Capabilities: [],
            WeeklySchedule: new WeeklyScheduleDto([])
        );
    }

    private static UpdatePlayerProfileRequest CreateUpdateRequest(string name)
    {
        return new UpdatePlayerProfileRequest(
            Name: name,
            SkillTimeMultiplier: 1.0m,
            Capabilities: [],
            WeeklySchedule: new WeeklyScheduleDto([])
        );
    }
}
