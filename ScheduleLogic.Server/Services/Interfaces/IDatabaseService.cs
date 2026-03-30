using Microsoft.AspNetCore.Identity;
using ScheduleLogic.Server.Models;
using static ScheduleLogic.Server.Class.EventModels;
using static ScheduleLogic.Server.Class.ScheduleModels;

namespace ScheduleLogic.Server.Services.Interfaces
{
    public interface IDatabaseService
    {
        public Task<string> LoginUser(string username, string password);
        public Task<string> RegisterUser(string username, string password, string email, AuthenticationService Auth_Service);
        public Task<long> NewEvent(string username);
        public Task<bool> NewWizardEvent(DataDTO Data, string username);
        public Task DeleteEvent(long id);
        public Task<long> GetUserID(string username);
        public Task<bool> CheckUser(string username, string id);
        public Task<bool> CheckUserExist(string username);
        public Task<List<EventsData>> GetEvents(string username);
        public Task<DataDTO> GetEvent(string id, string username);
        public Task<bool> SaveEvent(DataDTO Data);
        public Task<ScheduleRequestForSolver> GetScheduleInfo(int id);
        public Task<bool> NewSchedule(List<ScheduleModel> request, int Event_id);
        public Task<DataDTO> GetScheduleData(string id);
        public Task<ScheduleDataForEXPORT> GetScheduleDataEXPORT(string id);
        public Task<string> ValidateEmail(string token);
        public Task DeleteUser(string username);
    }
}
