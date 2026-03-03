using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Mvc;
using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Models;
using ScheduleLogic.Server.Services.Interfaces;
using static ScheduleLogic.Server.Class.EventModels;

namespace ScheduleLogic.Server.Services
{
    public class EventService : IEventService
    {
        private readonly IDatabaseService _dbService;

        public EventService(IDatabaseService dbService)
        {
            _dbService = dbService;
        }
        public async Task<bool> NewEvent(string username)
        {
            await _dbService.NewEvent(username);
            return true;
        }

        public async Task<bool> DeleteEvent(string id, string username)
        {
            var check = await _dbService.CheckUser(username, id);
            if (check == false) return false; 
            await _dbService.DeleteEvent(Convert.ToInt32(id));
            return true;
        }

        public async Task<List<EventsData>> GetEvents(string username)
        {
            return await _dbService.GetEvents(username);
        }

        public async Task<DataDTO?> GetEvent(string id, string username)
        {
            if (await _dbService.CheckUser(username, id) == false) return null;
            return await _dbService.GetEvent(id, username);
        }

        public async Task<bool> SaveEvent(DataDTO Data, string username)
        {
            if (await _dbService.CheckUser(username, Data.EventData.EventId.ToString()) == false) return false;
            if (await _dbService.SaveEvent(Data)) return true;
            else return false;
        }

        public async Task<bool> NewWizardEvent(DataDTO Data, string username)
        {
            await _dbService.NewWizardEvent(Data, username);
            return true;
        }
    }
}
