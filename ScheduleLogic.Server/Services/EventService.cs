using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Services.Interfaces;
using static ScheduleLogic.Server.Class.EventModels;

namespace ScheduleLogic.Server.Services
{
    public class EventService : IEventService
    {
        private readonly IEventRepository _events;

        public EventService(IEventRepository events)
        {
            _events = events;
        }

        public async Task NewEvent(long userId)
        {
            await _events.CreateEventAsync(userId);
        }

        public async Task<bool> DeleteEvent(long eventId, long userId)
        {
            if (!await _events.IsOwnedByUserAsync(eventId, userId)) return false;
            await _events.DeleteEventAsync(eventId);
            return true;
        }

        public Task<List<EventsData>> GetEvents(long userId) =>
            _events.GetEventsAsync(userId);

        public async Task<DataDTO?> GetEvent(long eventId, long userId)
        {
            if (!await _events.IsOwnedByUserAsync(eventId, userId)) return null;
            return await _events.GetEventAsync(eventId);
        }

        public async Task<bool> SaveEvent(DataDTO data, long userId)
        {
            if (!long.TryParse(data.EventData.EventId, out long eventId)) return false;
            if (!await _events.IsOwnedByUserAsync(eventId, userId)) return false;
            return await _events.SaveEventAsync(data);
        }

        public Task<bool> NewWizardEvent(DataDTO data, long userId) =>
            _events.CreateWizardEventAsync(userId, data);
    }
}