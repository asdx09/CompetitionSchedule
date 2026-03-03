using static ScheduleLogic.Server.Class.EventModels;

namespace ScheduleLogic.Server.Services.Interfaces
{
    public interface IEventService
    {
        public Task<bool> NewEvent(string username);

        public Task<bool> DeleteEvent(string id, string username);

        public Task<List<EventsData>> GetEvents(string username);

        public Task<DataDTO?> GetEvent(string id, string username);

        public Task<bool> SaveEvent(DataDTO Data, string username);

        public Task<bool> NewWizardEvent(DataDTO Data, string username);
    }
}
