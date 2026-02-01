using BingoSim.Application.DTOs;
using BingoSim.Application.Services;
using BingoSim.Core.Entities;
using BingoSim.Core.Exceptions;
using BingoSim.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BingoSim.Application.UnitTests.Services;

public class ActivityDefinitionServiceTests
{
    private readonly IActivityDefinitionRepository _repository;
    private readonly ILogger<ActivityDefinitionService> _logger;
    private readonly ActivityDefinitionService _service;

    public ActivityDefinitionServiceTests()
    {
        _repository = Substitute.For<IActivityDefinitionRepository>();
        _logger = Substitute.For<ILogger<ActivityDefinitionService>>();
        _service = new ActivityDefinitionService(_repository, _logger);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllActivities()
    {
        var entities = new List<ActivityDefinition>
        {
            CreateTestEntity("key1", "Name1"),
            CreateTestEntity("key2", "Name2")
        };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(entities);

        var result = await _service.GetAllAsync();

        result.Should().HaveCount(2);
        result[0].Key.Should().Be("key1");
        result[1].Key.Should().Be("key2");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsActivity()
    {
        var entity = CreateTestEntity("key1", "Name1");
        _repository.GetByIdAsync(entity.Id, Arg.Any<CancellationToken>()).Returns(entity);

        var result = await _service.GetByIdAsync(entity.Id);

        result.Should().NotBeNull();
        result!.Key.Should().Be("key1");
        result.Name.Should().Be("Name1");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((ActivityDefinition?)null);

        var result = await _service.GetByIdAsync(id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesAndReturnsId()
    {
        var request = CreateValidCreateRequest("new.key", "New Activity");
        _repository.GetByKeyAsync("new.key", Arg.Any<CancellationToken>()).Returns((ActivityDefinition?)null);

        var id = await _service.CreateAsync(request);

        id.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AddAsync(
            Arg.Is<ActivityDefinition>(e => e.Key == "new.key" && e.Name == "New Activity"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_KeyAlreadyExists_ThrowsKeyAlreadyExistsException()
    {
        var request = CreateValidCreateRequest("existing.key", "Activity");
        var existing = CreateTestEntity("existing.key", "Existing");
        _repository.GetByKeyAsync("existing.key", Arg.Any<CancellationToken>()).Returns(existing);

        var act = async () => await _service.CreateAsync(request);

        await act.Should().ThrowAsync<ActivityDefinitionKeyAlreadyExistsException>()
            .Where(e => e.Key == "existing.key");
        await _repository.DidNotReceive().AddAsync(Arg.Any<ActivityDefinition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ExistingId_UpdatesEntity()
    {
        var entity = CreateTestEntity("key1", "OldName");
        _repository.GetByIdAsync(entity.Id, Arg.Any<CancellationToken>()).Returns(entity);
        _repository.GetByKeyAsync("key1", Arg.Any<CancellationToken>()).Returns(entity);

        var request = CreateValidUpdateRequest("key1", "NewName");
        await _service.UpdateAsync(entity.Id, request);

        await _repository.Received(1).UpdateAsync(
            Arg.Is<ActivityDefinition>(e => e.Name == "NewName"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NonExistingId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((ActivityDefinition?)null);

        var request = CreateValidUpdateRequest("key", "Name");
        var act = async () => await _service.UpdateAsync(id, request);

        await act.Should().ThrowAsync<ActivityDefinitionNotFoundException>()
            .Where(e => e.ActivityDefinitionId == id);
    }

    [Fact]
    public async Task UpdateAsync_KeyTakenByOtherId_ThrowsKeyAlreadyExistsException()
    {
        var entity = CreateTestEntity("key1", "Name1");
        var other = CreateTestEntity("other.key", "Other");
        _repository.GetByIdAsync(entity.Id, Arg.Any<CancellationToken>()).Returns(entity);
        _repository.GetByKeyAsync("other.key", Arg.Any<CancellationToken>()).Returns(other);

        var request = CreateValidUpdateRequest("other.key", "Name1");

        var act = async () => await _service.UpdateAsync(entity.Id, request);

        await act.Should().ThrowAsync<ActivityDefinitionKeyAlreadyExistsException>()
            .Where(e => e.Key == "other.key");
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_DeletesEntity()
    {
        var id = Guid.NewGuid();
        _repository.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        await _service.DeleteAsync(id);

        await _repository.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _repository.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await _service.DeleteAsync(id);

        await act.Should().ThrowAsync<ActivityDefinitionNotFoundException>()
            .Where(e => e.ActivityDefinitionId == id);
    }

    private static ActivityDefinition CreateTestEntity(string key, string name)
    {
        var modeSupport = new Core.ValueObjects.ActivityModeSupport(true, true, null, null);
        var entity = new ActivityDefinition(key, name, modeSupport);
        var attempt = new Core.ValueObjects.ActivityAttemptDefinition(
            "attempt_1",
            Core.Enums.RollScope.PerPlayer,
            new Core.ValueObjects.AttemptTimeModel(60, Core.Enums.TimeDistribution.Uniform),
            [new Core.ValueObjects.ActivityOutcomeDefinition("outcome_1", 1, 1, [new Core.ValueObjects.ProgressGrant("drop.key", 1)])]);
        entity.SetAttempts([attempt]);
        return entity;
    }

    private static CreateActivityDefinitionRequest CreateValidCreateRequest(string key, string name)
    {
        return new CreateActivityDefinitionRequest(
            Key: key,
            Name: name,
            ModeSupport: new ActivityModeSupportDto(true, true, null, null),
            Attempts: [
                new ActivityAttemptDefinitionDto(
                    "attempt_1",
                    0,
                    new AttemptTimeModelDto(60, 0, null),
                    [new ActivityOutcomeDefinitionDto("outcome_1", 1, 1, [new ProgressGrantDto("drop.key", 1)])])
            ],
            GroupScalingBands: []);
    }

    private static UpdateActivityDefinitionRequest CreateValidUpdateRequest(string key, string name)
    {
        return new UpdateActivityDefinitionRequest(
            Key: key,
            Name: name,
            ModeSupport: new ActivityModeSupportDto(true, true, null, null),
            Attempts: [
                new ActivityAttemptDefinitionDto(
                    "attempt_1",
                    0,
                    new AttemptTimeModelDto(60, 0, null),
                    [new ActivityOutcomeDefinitionDto("outcome_1", 1, 1, [new ProgressGrantDto("drop.key", 1)])])
            ],
            GroupScalingBands: []);
    }
}
