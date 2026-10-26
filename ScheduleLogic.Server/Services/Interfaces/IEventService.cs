using static ScheduleLogic.Server.Class.EventModels;

namespace ScheduleLogic.Server.Services.Interfaces
{
    public interface IEventService
    {
        Task NewEvent(long userId);
        Task<bool> DeleteEvent(long eventId, long userId);
        Task<List<EventsData>> GetEvents(long userId);
        Task<DataDTO?> GetEvent(long eventId, long userId);
        Task<bool> SaveEvent(DataDTO data, long userId);
        Task<bool> NewWizardEvent(DataDTO data, long userId);
    }
}
