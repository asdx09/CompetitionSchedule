using static ScheduleLogic.Server.Class.EventModels;

namespace ScheduleLogic.Server.Services.Interfaces
{
    public interface IEventRepository
    {
        Task<long> CreateEventAsync(long userId);
        Task<bool> IsOwnedByUserAsync(long eventId, long userId);
        Task DeleteEventAsync(long eventId);
        Task<List<EventsData>> GetEventsAsync(long userId);
        Task<DataDTO?> GetEventAsync(long eventId);
        Task<bool> SaveEventAsync(DataDTO data);
        Task<bool> FillWizardEventAsync(long eventId, DataDTO data);
    }
}
