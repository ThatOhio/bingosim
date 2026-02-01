using BingoSim.Application.DTOs;
using BingoSim.Application.Services;
using BingoSim.Core.Entities;
using BingoSim.Core.Exceptions;
using BingoSim.Core.Interfaces;
using BingoSim.Core.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BingoSim.Application.UnitTests.Services;

public class EventServiceTests
{
    private readonly IEventRepository _eventRepository;
    private readonly IActivityDefinitionRepository _activityRepository;
    private readonly ILogger<EventService> _logger;
    private readonly EventService _service;

    public EventServiceTests()
    {
        _eventRepository = Substitute.For<IEventRepository>();
        _activityRepository = Substitute.For<IActivityDefinitionRepository>();
        _logger = Substitute.For<ILogger<EventService>>();
        _service = new EventService(_eventRepository, _activityRepository, _logger);
    }

    private static CreateEventRequest CreateValidRequest(string name = "Test Event")
    {
        var activityId = Guid.NewGuid();
        var row = new RowDto(0, [
            new TileDto("r0.p1", "Tile 1", 1, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])]),
            new TileDto("r0.p2", "Tile 2", 2, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])]),
            new TileDto("r0.p3", "Tile 3", 3, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])]),
            new TileDto("r0.p4", "Tile 4", 4, 1, [new TileActivityRuleDto(activityId, "activity.key", null, [], [], [])])
        ]);
        return new CreateEventRequest(name, TimeSpan.FromHours(24), 5, [row]);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEvents()
    {
        var evt = CreateTestEvent();
        _eventRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([evt]);
        _activityRepository.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);

        var result = await _service.GetAllAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be(evt.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsEvent()
    {
        var evt = CreateTestEvent();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);
        _activityRepository.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);

        var result = await _service.GetByIdAsync(evt.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be(evt.Name);
        result.Rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _eventRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Event?)null);

        var result = await _service.GetByIdAsync(id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ResolvesActivityKeysAndCreatesEvent()
    {
        var activityId = Guid.NewGuid();
        var activity = new ActivityDefinition("activity.key", "Activity Name", new ActivityModeSupport(true, true, null, null));
        var request = CreateValidRequest();
        _activityRepository.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([activity]);

        var id = await _service.CreateAsync(request);

        id.Should().NotBe(Guid.Empty);
        await _eventRepository.Received(1).AddAsync(
            Arg.Is<Event>(e => e.Name == "Test Event" && e.Rows.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ExistingEvent_UpdatesAndPersists()
    {
        var evt = CreateTestEvent();
        var activity = new ActivityDefinition("activity.key", "Activity Name", new ActivityModeSupport(true, true, null, null));
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);
        _activityRepository.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([activity]);
        var createRequest = CreateValidRequest();
        var updateRequest = new UpdateEventRequest("Updated Name", TimeSpan.FromHours(48), 10, createRequest.Rows);

        await _service.UpdateAsync(evt.Id, updateRequest);

        await _eventRepository.Received(1).UpdateAsync(
            Arg.Is<Event>(e => e.Id == evt.Id && e.Name == "Updated Name"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NonExistingEvent_ThrowsEventNotFoundException()
    {
        var id = Guid.NewGuid();
        _eventRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Event?)null);
        var request = CreateValidRequest();

        var act = async () => await _service.UpdateAsync(id, new UpdateEventRequest(request.Name, request.Duration, request.UnlockPointsRequiredPerRow, request.Rows));

        await act.Should().ThrowAsync<EventNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_ExistingEvent_Deletes()
    {
        var id = Guid.NewGuid();
        _eventRepository.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        await _service.DeleteAsync(id);

        await _eventRepository.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NonExistingEvent_ThrowsEventNotFoundException()
    {
        var id = Guid.NewGuid();
        _eventRepository.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await _service.DeleteAsync(id);

        await act.Should().ThrowAsync<EventNotFoundException>();
    }

    private static Event CreateTestEvent()
    {
        var evt = new Event("Test Event", TimeSpan.FromHours(24), 5);
        var activityId = Guid.NewGuid();
        var rule = new TileActivityRule(activityId, "activity.key", [], [], []);
        var row = new Row(0, [
            new Tile("r0.p1", "Tile 1", 1, 1, [rule]),
            new Tile("r0.p2", "Tile 2", 2, 1, [rule]),
            new Tile("r0.p3", "Tile 3", 3, 1, [rule]),
            new Tile("r0.p4", "Tile 4", 4, 1, [rule])
        ]);
        evt.SetRows([row]);
        return evt;
    }
}
