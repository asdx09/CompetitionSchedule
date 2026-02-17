using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using ScheduleLogic.Server.Controllers;
using Microsoft.Extensions.Logging;

namespace ScheduleLogic.Server.Services
{
    //Backend to SOLVER
    public class LocationModel
    {
        public long id {  get; set; }
        public string name { get; set; }
        public int capacity { get; set; }
    }
    public class EventModel
    {
        public long id { get; set; }
        public string name { get; set; }
        public int duration { get; set; }
        public List<long> possible_locations { get; set; } = new List<long>();
    }
    public class CompetitorModel
    {
        public long id { get; set; }
        public string name { get; set; }
        public long group_id { get; set; }
    }
    public class EntryModel
    {
        public long id { get; set; }
        public long competitor_id { get; set; }
        public long event_id { get; set; }
    }
    public class GroupModel
    {
        public long id { get; set; }
        public string name { get; set; }
        public long event_id { get; set; }
    }
    public class ConstraintModel
    {
        public long id { get; set; }
        public long object_id { get; set; }
        public string ConstraintType { get; set; } //'L' location, 'C' competitor, 'E' event, 'G' Group
        public int StartTime { get; set; }
        public int EndTime { get; set; }
    }

    public partial class PauseTableModel
    {
        public long id { get; set; }
        public long LocationId1 { get; set; }
        public long LocationId2 { get; set; }
        public int Pause { get; set; }
    }

    public class ScheduleRequestForSolver
    {
        public string ReturnURL { get; set; } = "https://localhost:7098/api/Schedule/answer";
        public int event_id { get; set; }
        public List<LocationModel> locations { get; set; }
        public List<EventModel> events { get; set; }
        public List<CompetitorModel> competitors {  get; set; }
        public List<EntryModel> entries { get; set; }
        public List<PauseTableModel> travel {  get; set; }
        public List<ConstraintModel> constraints { get; set; }
        public int day_length { get; set; }
        public int max_days { get; set; }
        public int break_time_loc { get; set; }
        public int base_pause_time { get; set; }
        public int locWeight { get; set; }
        public int groupWeight { get; set; }
        public int typeWeight { get; set; }
        public int compWeight { get; set; }
    }

    //Backend to frontend
    public class ScheduleDataForFrontEnd
    {
        public List<scheduleTimeZone> timeZones { get; set; }
        public int event_ID { get; set; }
        public string eventName { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
        public List<scheduleEventType> eventTypes { get; set; }
        public List<scheduleParticipans> participans { get; set; }
        public List<scheduleLocations> locations { get; set; }
        public List<scheduleConstraint> constraints { get; set; }
    }

    public class scheduleTimeZone
    {
        public long schedule_ID { get; set; }
        public long eventType_ID { get; set; }
        public long participant_ID { get; set; }
        public long location_ID { get; set; }
        public int StartTime { get; set; }
        public int EndTime { get; set; }
        public int Slot {  get; set; }
    }

    public class scheduleEventType
    {
        public long eventType_ID { get; set; }
        public string eventTypeName { get; set; }
    }

    public class scheduleParticipans
    {
        public long participant_ID { get; set; }
        public string participantName { get; set; }
    }

    public class scheduleLocations
    {
        public long location_ID { get; set; }
        public string locationName { get; set; }
    }
    public class scheduleConstraint
    {
        public long id { get; set; }
        public long object_ID { get; set; }
        public string ConstraintType { get; set; } //'L' location, 'C' competitor, 'E' event, 'G' Group
        public int StartTime { get; set; }
        public int EndTime { get; set; }
    }

    //SOLVER to backend
    public class SolverResponse
    {
        public int event_id { get; set; }
        public string status { get; set; }
        public List<Schedule> schedule { get; set; }
    }

    public class Schedule
    {
        public int participant_id { get; set; }
        public int eventtype_id { get; set; }
        public int location_id { get; set; }
        public int start { get; set; }
        public int end { get; set; }
        public int slot { get; set; }
    }

    public class SolverStatusResponse
    {
        public bool running { get; set; }
    }

    public class StopSolverResponse
    {
        public string Status { get; set; }
    }

    public class ScheduleService
    {
        private readonly DatabaseService _dbService;
        string apiUrl = "http://127.0.0.1:8000/";

        public ScheduleService(DatabaseService dbService)
        {
            _dbService = dbService;
        }
        public async Task<ScheduleRequestForSolver> GenerateSchedule(int id)
        {
            var SR = _dbService.GetScheduleInfo(id);
            using var client = new HttpClient();

            var httpResponse = await client.PostAsJsonAsync(apiUrl + "schedule", SR);

            if (httpResponse.IsSuccessStatusCode)
            {
                var jsonString = await httpResponse.Content.ReadAsStringAsync();
            }
            else
            {
                //"API call error: " + httpResponse.StatusCode;
            }
            return SR;
        }

        public async Task<ScheduleDataForFrontEnd> GetScheduleData(string id)
        {
            return _dbService.GetScheduleData(id);
        }

        public async Task<bool> CheckSolver(string id)
        {
            using var client = new HttpClient();

            var response = await client.GetAsync(apiUrl+"is_solver_running?event_id="+id);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SolverStatusResponse>(jsonString);

                return result?.running ?? false;
            }
            else
            {
                Console.WriteLine("API call error: " + response.StatusCode);
                return false;
            }
        }

        public async Task<bool> StopSolver(string id)
        {
            using var client = new HttpClient();

            var response = await client.GetAsync(apiUrl + "stop_solver?event_id=" + id);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<StopSolverResponse>(jsonString);

                return result?.Status == "STOPPED";
            }
            else
            {
                Console.WriteLine("API call error: " + response.StatusCode);
                return false;
            }
        }
    }
}
