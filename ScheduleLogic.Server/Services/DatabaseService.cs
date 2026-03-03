using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2016.Drawing.Charts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Controllers;
using ScheduleLogic.Server.Models;
using ScheduleLogic.Server.Services.Interfaces;
using System;
using System.Linq;
using static ScheduleLogic.Server.Class.EventModels;
using static ScheduleLogic.Server.Class.ScheduleModels;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ScheduleLogic.Server.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly ScheduleLogicDbContext _dbService;

        public DatabaseService(ScheduleLogicDbContext context)
        {
            _dbService = context;
        }

        public async Task<bool> LoginUser(string username, string password)
        {
            var user = await _dbService.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return false;

            var passwordHasher = new PasswordHasher<User>();
            var result = passwordHasher.VerifyHashedPassword(user, user.Password, password);

            return result == PasswordVerificationResult.Success;
        }
        public async Task<string> RegisterUser(string username, string password, string email)
        {
            if (await _dbService.Users.AnyAsync(u => u.Username == username))
                return "Username already taken!";

            var passwordHasher = new PasswordHasher<User>();
            var user = new User
            {
                Username = username
            };
            user.Password = passwordHasher.HashPassword(user, password);
            user.Email = email;
            user.Validated = true;

            await _dbService.Users.AddAsync(user);
            await _dbService.SaveChangesAsync();
            return "";
        }
        public async Task<long> NewEvent(string username)
        {
            Event newEvent = new Event
            {
                CreatedBy = await GetUserID(username),
                EventName = "New Event",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now,
                BasePauseTime = 5,
                LocationPauseTime = 5,
                IsPrivate = true,
                LocWeight = 1,
                CompWeight = 1,
                GroupWeight = 1,
                TypeWeight = 1
            };
            var tempEvent = await _dbService.Events.AddAsync(newEvent);
            await _dbService.SaveChangesAsync();
            return tempEvent.Entity.EventId;
        }
        public async Task<bool> NewWizardEvent(DataDTO Data, string username)
        {
            using var tx = _dbService.Database.BeginTransaction();

            long id = await NewEvent(username);

            var existingEvent = await _dbService.Events
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (existingEvent != null)
            {
                existingEvent.EventName = Data.EventData.EventName;
                existingEvent.StartDate = Data.EventData.StartDate;
                existingEvent.EndDate = Data.EventData.EndDate;
                existingEvent.IsPrivate = Data.EventData.IsPrivate;
                existingEvent.BasePauseTime = Data.EventData.BasePauseTime;
                existingEvent.LocationPauseTime = Data.EventData.LocationPauseTime;
            }


            var EventTypeMap = new Dictionary<string, EventType>();
            foreach (EventTypeDTO item in Data.EventTypes)
            {
                EventType temp = new EventType();
                temp.EventId = id;
                temp.TypeName = item.TypeName;
                temp.TimeRange = item.TimeRange;
                _dbService.EventTypes.Add(temp);
                EventTypeMap[item.EventTypeId] = temp;
            }

            var GroupMap = new Dictionary<string, Group>();
            foreach (GroupDTO item in Data.Groups)
            {
                Group temp = new Group();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.GroupName = item.GroupName;
                _dbService.Groups.Add(temp);
                GroupMap[item.GroupId] = temp;
            }

            var LocationMap = new Dictionary<string, Location>();
            foreach (LocationDTO item in Data.Locations)
            {
                Location temp = new Location();
                temp.LocationName = item.LocationName;
                temp.EventId = (long)Convert.ToDouble(id);
                temp.Slots = item.Slots;
                _dbService.Locations.Add(temp);
                LocationMap[item.LocationId] = temp;
            }

            var LocationTableMap = new Dictionary<string, LocationTable>();
            foreach (LocationTableDTO item in Data.LocationTable)
            {
                LocationTable temp = new LocationTable();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.LocationId = LocationMap[item.LocationId].LocationId;
                temp.EventTypeId = EventTypeMap[item.EventTypeId].EventTypeId;
                _dbService.LocationTables.Add(temp);
                LocationTableMap[item.LocationTableId] = temp;
            }

            var ParticipantMap = new Dictionary<string, Participant>();
            foreach (ParticipantDTO item in Data.Participants)
            {
                Participant temp = new Participant();
                temp.ParticipantName = item.ParticipantName;
                temp.EventId = (long)Convert.ToDouble(id);
                temp.CompetitorNumber = item.CompetitorNumber;
                if (item.GroupId != null && item.GroupId != "") temp.GroupId = GroupMap[item.GroupId].GroupId;
                _dbService.Participants.Add(temp);
                ParticipantMap[item.ParticipantId] = temp;
            }

            var pauseTableMap = new Dictionary<string, PauseTable>();
            foreach (PauseTableDTO item in Data.PauseTable)
            {
                PauseTable temp = new PauseTable();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.LocationId1 = LocationMap[item.LocationId1].LocationId;
                temp.LocationId2 = LocationMap[item.LocationId2].LocationId;
                temp.Pause = item.Pause;
                _dbService.PauseTables.Add(temp);
                pauseTableMap[item.PauseId] = temp;
            }

            var registrationMap = new Dictionary<string, Registration>();
            foreach (RegistrationDTO item in Data.Registrations)
            {
                Registration temp = new Registration();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.ParticipantId = ParticipantMap[item.ParticipantId].ParticipantId;
                temp.EventTypeId = EventTypeMap[item.EventTypeId].EventTypeId;
                _dbService.Registrations.Add(temp);
                registrationMap[item.RegistrationId] = temp;
            }

            var ConstraintMap = new Dictionary<string, Constraint>();
            foreach (ConstraintDTO item in Data.Constraints)
            {
                Constraint temp = new Constraint();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.ConstraintType = item.ConstraintType;
                try
                {
                    switch (temp.ConstraintType)
                    {
                        case "L":
                            temp.ObjectId = LocationMap[item.ObjectId].LocationId;
                            break;
                        case "G":
                            temp.ObjectId = GroupMap[item.ObjectId].GroupId;
                            break;
                        case "C":
                            temp.ObjectId = ParticipantMap[item.ObjectId].ParticipantId;
                            break;
                        case "T":
                            temp.ObjectId = EventTypeMap[item.ObjectId].EventTypeId;
                            break;
                    }
                }
                catch { return false; }
                temp.StartTime = item.StartTime;
                temp.EndTime = item.EndTime;
                _dbService.Constraints.Add(temp);
                ConstraintMap[item.ConstraintId] = temp;
            }
            await _dbService.SaveChangesAsync(); 

            try
            {
                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return false;
            }
            return true;
        }
        public async Task DeleteEvent(int id)
        {
            var EventToRemove = await _dbService.Events.Where(w => w.EventId == id).ToListAsync();
            _dbService.Events.RemoveRange(EventToRemove);
            var pauesToRemove = await _dbService.PauseTables.Where(w => w.EventId == id).ToListAsync();
            _dbService.PauseTables.RemoveRange(pauesToRemove);
            var LocationTableToRemove = await _dbService.LocationTables.Where(w => w.EventId == id).ToListAsync();
            _dbService.LocationTables.RemoveRange(LocationTableToRemove);
            var LocationsToRemove = await _dbService.Locations.Where(w => w.EventId == id).ToListAsync();
            _dbService.Locations.RemoveRange(LocationsToRemove);
            var SchedulesToRemove = await _dbService.Schedules.Where(w => w.EventTypeId == id).ToListAsync();
            _dbService.Schedules.RemoveRange(SchedulesToRemove);
            var registrationsToRemove = await _dbService.Registrations.Where(w => w.EventId == id).ToListAsync();
            _dbService.Registrations.RemoveRange(registrationsToRemove);
            var ParticipantsToRemove = await _dbService.Participants.Where(w => w.EventId == id).ToListAsync();
            _dbService.Participants.RemoveRange(ParticipantsToRemove);
            var GroupsToRemove = await _dbService.Groups.Where(w => w.EventId == id).ToListAsync();
            _dbService.Groups.RemoveRange(GroupsToRemove);
            var EventTypesToRemove = await _dbService.EventTypes.Where(w => w.EventId == id).ToListAsync();
            _dbService.EventTypes.RemoveRange(EventTypesToRemove);
            var constraintsToRemove = await _dbService.Constraints.Where(w => w.EventId == id).ToListAsync();
            _dbService.Constraints.RemoveRange(constraintsToRemove);
            await _dbService.SaveChangesAsync();
        }
        public async Task<long> GetUserID(string username)
        {
            if (await _dbService.Users.CountAsync() < 1) return 0;
            return _dbService.Users.Where(w => w.Username == username).First().UserId;
        }
        public async Task<bool> CheckUser(string username, string id)
        {
            var userId = await GetUserID(username);
            return await _dbService.Events.Where(w => w.CreatedBy == userId && w.EventId == Convert.ToInt32(id)).AnyAsync();
        }
        public async Task<bool> CheckUserExist(string username)
        {
            return await _dbService.Users.Where(w => w.Username == username).CountAsync() > 0;
        }
        public async Task<List<EventsData>> GetEvents(string username)
        {
            var userId = await GetUserID(username);
            return await _dbService.Events.Where(e => e.CreatedBy == userId).Select(e => new EventsData{EventId = e.EventId, EventName = e.EventName}).ToListAsync();
        }
        public async Task<DataDTO> GetEvent(string id, string username)
        {
            DataDTO TempData = new DataDTO();
            var userId = await GetUserID(username);
            TempData.EventData = await _dbService.Events.AsNoTracking().Where(w => w.CreatedBy == userId && w.EventId == Convert.ToInt32(id)).Select(e => new EventDTO
            {
                EventId = e.EventId.ToString(),
                EventName = e.EventName,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                IsPrivate = e.IsPrivate,
                BasePauseTime = e.BasePauseTime,
                LocationPauseTime = e.LocationPauseTime,
                LocWeight = e.LocWeight,
                GroupWeight = e.GroupWeight,
                TypeWeight = e.TypeWeight,
                CompWeight = e.CompWeight
            }).FirstAsync();
            TempData.EventTypes = await _dbService.EventTypes.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new EventTypeDTO
            {
                EventId = e.EventId.ToString(),
                EventTypeId = e.EventTypeId.ToString(),
                TypeName = e.TypeName,
                TimeRange = e.TimeRange
            }).ToListAsync();
            TempData.Groups = await _dbService.Groups.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new GroupDTO
            {
                GroupId = e.GroupId.ToString(),
                EventId = e.EventId.ToString(),
                GroupName = e.GroupName
            }).ToListAsync();
            TempData.Locations = await _dbService.Locations.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new LocationDTO
            {
                LocationId = e.LocationId.ToString(),
                EventId = e.EventId.ToString(),
                LocationName = e.LocationName,
                Slots = e.Slots
            }).ToListAsync();
            TempData.Participants = await _dbService.Participants.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new ParticipantDTO
            {
                ParticipantId = e.ParticipantId.ToString(),
                ParticipantName = e.ParticipantName,
                CompetitorNumber = e.CompetitorNumber,
                EventId = e.EventId.ToString(),
                GroupId = e.GroupId.ToString()
            }).ToListAsync();
            TempData.Registrations = await _dbService.Registrations.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new RegistrationDTO
            {
                RegistrationId = e.RegistrationId.ToString(),
                EventId= e.EventId.ToString(),
                ParticipantId= e.ParticipantId.ToString(),
                EventTypeId = e.EventTypeId.ToString()
            }).ToListAsync();
            TempData.PauseTable = await _dbService.PauseTables.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new PauseTableDTO
            {
                PauseId = e.PauseId.ToString(),
                EventId = e.EventId.ToString(),
                LocationId1 = e.LocationId1.ToString(),
                LocationId2 = e.LocationId2.ToString(),
                Pause = e.Pause
            }).ToListAsync();
            TempData.LocationTable = await _dbService.LocationTables.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new LocationTableDTO
            {
                LocationTableId = e.LocationTableId.ToString(),
                EventId = e.EventId.ToString(),
                EventTypeId = e.EventTypeId.ToString(),
                LocationId = e.LocationId.ToString()
            }).ToListAsync();
            TempData.Constraints = await _dbService.Constraints.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new ConstraintDTO
            {
                ConstraintId = e.ConstraintId.ToString(),
                EventId = e.EventId.ToString(),
                ObjectId = e.ObjectId.ToString(),
                ConstraintType = e.ConstraintType.ToString(),
                StartTime = e.StartTime,
                EndTime = e.EndTime
            }).ToListAsync();
            return TempData;
        }
        public async Task<bool> SaveEvent(DataDTO Data)
        {
            using var tx = _dbService.Database.BeginTransaction();

            long id = (long)Convert.ToDouble(Data.EventData.EventId);

            var existingEvent = await _dbService.Events
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (existingEvent != null)
            {
                existingEvent.EventName = Data.EventData.EventName;
                existingEvent.StartDate = Data.EventData.StartDate;
                existingEvent.EndDate = Data.EventData.EndDate;
                existingEvent.IsPrivate = Data.EventData.IsPrivate;
                existingEvent.BasePauseTime = Data.EventData.BasePauseTime;
                existingEvent.LocationPauseTime = Data.EventData.LocationPauseTime;
                existingEvent.LocWeight = Data.EventData.LocWeight;
                existingEvent.GroupWeight = Data.EventData.GroupWeight;
                existingEvent.TypeWeight = Data.EventData.TypeWeight;
                existingEvent.CompWeight = Data.EventData.CompWeight;
            }

            var EventTypesToRemove = _dbService.EventTypes.Where(w => w.EventId == id).ToList();
            _dbService.EventTypes.RemoveRange(EventTypesToRemove);
            var GroupsToRemove = _dbService.Groups.Where(w => w.EventId == id).ToList();
            _dbService.Groups.RemoveRange(GroupsToRemove);
            var LocationsToRemove = _dbService.Locations.Where(w => w.EventId == id).ToList();
            _dbService.Locations.RemoveRange(LocationsToRemove);
            var LocationTableToRemove = _dbService.LocationTables.Where(w => w.EventId == id).ToList();
            _dbService.LocationTables.RemoveRange(LocationTableToRemove);
            var ParticipantsToRemove = _dbService.Participants.Where(w => w.EventId == id).ToList();
            _dbService.Participants.RemoveRange(ParticipantsToRemove);
            var PauseTableToRemove = _dbService.PauseTables.Where(w => w.EventId == id).ToList();
            _dbService.PauseTables.RemoveRange(PauseTableToRemove);
            var RegistrationToRemove = _dbService.Registrations.Where(w => w.EventId == id).ToList();
            _dbService.Registrations.RemoveRange(RegistrationToRemove);
            var ConstraintsToRemove = _dbService.Constraints.Where(w => w.EventId == id).ToList();
            _dbService.Constraints.RemoveRange(ConstraintsToRemove);

            var EventTypeMap = new Dictionary<string, EventType>();
            foreach (EventTypeDTO item in Data.EventTypes)
            {
                EventType temp = new EventType();
                temp.EventId = id;
                temp.TypeName = item.TypeName;
                temp.TimeRange = item.TimeRange;
                _dbService.EventTypes.Add(temp);
                EventTypeMap[item.EventTypeId] = temp;
            }

            var GroupMap = new Dictionary<string, Group>();
            foreach (GroupDTO item in Data.Groups)
            {
                Group temp = new Group();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.GroupName = item.GroupName;
                _dbService.Groups.Add(temp);
                GroupMap[item.GroupId] = temp;
            }

            var LocationMap = new Dictionary<string, Location>();
            foreach (LocationDTO item in Data.Locations)
            {
                Location temp = new Location();
                temp.LocationName = item.LocationName;
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.Slots = item.Slots;
                _dbService.Locations.Add(temp);
                LocationMap[item.LocationId] = temp;
            }

            var LocationTableMap = new Dictionary<string, LocationTable>();
            foreach (LocationTableDTO item in Data.LocationTable)
            {
                LocationTable temp = new LocationTable();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.LocationId = LocationMap[item.LocationId].LocationId;
                temp.EventTypeId = EventTypeMap[item.EventTypeId].EventTypeId;
                _dbService.LocationTables.Add(temp);
                LocationTableMap[item.LocationTableId] = temp;
            }

            var ParticipantMap = new Dictionary<string, Participant>();
            foreach (ParticipantDTO item in Data.Participants)
            {
                Participant temp = new Participant();
                temp.ParticipantName = item.ParticipantName;
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.CompetitorNumber = item.CompetitorNumber;
                if (item.GroupId != null && item.GroupId != "") temp.GroupId = GroupMap[item.GroupId].GroupId;
                _dbService.Participants.Add(temp);
                ParticipantMap[item.ParticipantId] = temp;
            }

            var pauseTableMap = new Dictionary<string, PauseTable>();
            foreach (PauseTableDTO item in Data.PauseTable)
            {
                PauseTable temp = new PauseTable();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.LocationId1 = LocationMap[item.LocationId1].LocationId;
                temp.LocationId2 = LocationMap[item.LocationId2].LocationId;
                temp.Pause = item.Pause;
                _dbService.PauseTables.Add(temp);
                pauseTableMap[item.PauseId] = temp;
            }

            var registrationMap = new Dictionary<string, Registration>();
            foreach (RegistrationDTO item in Data.Registrations)
            {
                Registration temp = new Registration();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.ParticipantId = ParticipantMap[item.ParticipantId].ParticipantId;
                temp.EventTypeId = EventTypeMap[item.EventTypeId].EventTypeId;
                _dbService.Registrations.Add(temp);
                registrationMap[item.RegistrationId] = temp;
            }

            var ConstraintMap = new Dictionary<string, Constraint>();
            foreach (ConstraintDTO item in Data.Constraints)
            {
                Constraint temp = new Constraint();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.ConstraintType = item.ConstraintType;
                try
                {
                    switch (temp.ConstraintType)
                    {
                        case "L":
                            temp.ObjectId = LocationMap[item.ObjectId].LocationId;
                            break;
                        case "G":
                            temp.ObjectId = GroupMap[item.ObjectId].GroupId;
                            break;
                        case "C":
                            temp.ObjectId = ParticipantMap[item.ObjectId].ParticipantId;
                            break;
                        case "T":
                            temp.ObjectId = EventTypeMap[item.ObjectId].EventTypeId;
                            break;
                    }
                } catch { return false; }
                temp.StartTime = item.StartTime;
                temp.EndTime = item.EndTime;
                _dbService.Constraints.Add(temp);
                ConstraintMap[item.ConstraintId] = temp;
            }
            await _dbService.SaveChangesAsync();

            try
            {
                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return false;
            }
            return true;
        }
        public async Task<ScheduleRequestForSolver> GetScheduleInfo(int id)
        {
            ScheduleRequestForSolver request = new ScheduleRequestForSolver();

            request.EventId = id;

            List<LocationModel> LML = new List<LocationModel>();
            foreach (Location item in _dbService.Locations.Where(w => w.EventId == id))
            {
                LocationModel LM = new LocationModel();
                LM.Id = item.LocationId;
                LM.Name = item.LocationName;
                LM.Capacity = Convert.ToInt32(item.Slots);
                LML.Add(LM);
            }
            request.Locations = LML;

            var EML = _dbService.EventTypes.Where(e => e.EventId == id).Select(e => new EventModel
            {
                Id = e.EventTypeId,
                Name = e.TypeName,
                Duration = e.TimeRange.Hour * 60 + e.TimeRange.Minute,
                PossibleLocations = _dbService.LocationTables
                    .Where(l => l.EventTypeId == e.EventTypeId)
                    .Select(l => l.LocationId)
                    .ToList()
            }).ToList();
            request.Events = EML;

            List<CompetitorModel> CML = new List<CompetitorModel>();
            foreach (Participant item in _dbService.Participants.Where(w => w.EventId == id))
            {
                CompetitorModel CM = new CompetitorModel();
                CM.Id = item.ParticipantId;
                CM.Name = item.ParticipantName;
                CM.GroupId = item.GroupId ?? -1;
                CML.Add(CM);
            }
            request.Competitors = CML;

            List<EntryModel> EnML = new List<EntryModel>();
            foreach (Registration item in _dbService.Registrations.Where(w => w.EventId == id))
            {
                EntryModel EnM = new EntryModel();
                EnM.Id = item.RegistrationId;
                EnM.EventId = item.EventTypeId;
                EnM.CompetitorId = item.ParticipantId;
                EnML.Add(EnM);
            }
            request.Entries = EnML;

            var EventStart = _dbService.Events.Where(w => w.EventId == id).Select(s => s.StartDate).First();
            List<ConstraintModel> CoML = new List<ConstraintModel>();
            foreach (Constraint item in _dbService.Constraints.Where(w => w.EventId == id))
            {
                ConstraintModel CoM = new ConstraintModel();
                CoM.Id = item.ConstraintId;
                CoM.ConstraintType = item.ConstraintType;
                CoM.ObjectId = item.ObjectId;
                CoM.StartTime = (int)(item.StartTime - EventStart).TotalMinutes;
                CoM.EndTime = (int)(item.EndTime - EventStart).TotalMinutes;
                CoML.Add(CoM);
            }
            request.Constraints = CoML;


            List<PauseTableModel> PL = new List<PauseTableModel>();
            foreach (PauseTable item in _dbService.PauseTables.Where(w => w.EventId == id))
            {
                PauseTableModel PM = new PauseTableModel();
                PM.Id = item.PauseId;
                PM.LocationID1 = item.LocationId1;
                PM.LocationID2 = item.LocationId2;
                PM.Pause = item.Pause.Hour * 60 + item.Pause.Minute;
                PL.Add(PM);
            }
            request.Travel = PL;

            request.DayLength = 24 * 60 - 1;
            request.MaxDays = (_dbService.Events.Where(w => w.EventId == id).First().EndDate - _dbService.Events.Where(w => w.EventId == id).FirstOrDefault().StartDate).Days + 1;
            request.BreakTimeLoc = _dbService.Events.Where(w => w.EventId == id).First().LocationPauseTime;
            request.BasePauseTime = _dbService.Events.Where(w => w.EventId == id).First().BasePauseTime;
            request.LocWeight = Math.Min(500,_dbService.Events.Where(w => w.EventId == id).First().LocWeight);
            request.GroupWeight = Math.Min(500, _dbService.Events.Where(w => w.EventId == id).First().GroupWeight);
            request.TypeWeight = Math.Min(500, _dbService.Events.Where(w => w.EventId == id).First().TypeWeight);
            request.CompWeight = Math.Min(500, _dbService.Events.Where(w => w.EventId == id).First().CompWeight);
            return request;
        }
        public async Task<bool> NewSchedule(List<ScheduleModel> request, int Event_id)
        {
            List<long> typeIDs = _dbService.EventTypes.Where(w => w.EventId == Event_id).Select(s => s.EventTypeId).ToList();
            DateTime EventStart = _dbService.Events.Where(w => w.EventId == Event_id).Select(s => s.StartDate).First();
            var itemsToRemove = _dbService.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)).ToList();
            _dbService.Schedules.RemoveRange(itemsToRemove);

            foreach (var item in request)
            {
                Models.Schedule NS = new Models.Schedule();
                NS.ParticipantId = item.ParticipantId;
                NS.StartTime = EventStart.AddMinutes(item.Start);
                NS.EndTime = EventStart.AddMinutes(item.End);
                NS.LocationId = item.LocationId;
                NS.Slot = item.Slot;
                NS.EventTypeId = item.EventTypeId;
                _dbService.Schedules.Add(NS);
            }

            try
            {
                _dbService.SaveChanges();
            }
            catch (Exception ex)
            {
                return false;
            }

            var Schedules = _dbService.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)).ToList();
            var pause = _dbService.Events.Where(w => w.EventId == Event_id).First().LocationPauseTime;
            var Locations = _dbService.Locations.ToList();

            var GroupedByLocation = Schedules.GroupBy(s => s.LocationId);

            foreach (var Group in GroupedByLocation)
            {
                var entries = Group.OrderBy(s => s.StartTime).ToList();
                var activeSlots = new List<Models.Schedule>();

                int max = Locations
                    .First(l => l.LocationId == Group.Key)
                    .Slots;

                foreach (var e in entries)
                {
                    activeSlots = activeSlots
                        .Where(a => a.EndTime + new TimeSpan(0, 5, 0) > e.StartTime)
                        .ToList();

                    for (int slot = 1; slot <= max; slot++)
                    {
                        if (!activeSlots.Any(a => a.Slot == slot))
                        {
                            e.Slot = slot;
                            activeSlots.Add(e);
                            break;
                        }
                    }
                }
            }


            try
            {
                _dbService.SaveChanges();
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }
        public async Task<DataDTO> GetScheduleData(string id)
        {
            DataDTO TempData = new DataDTO();
            TempData.EventData.EventId = id;
            TempData.EventData.EventName = _dbService.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EventName;
            TempData.EventData.StartDate = _dbService.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().StartDate;
            TempData.EventData.EndDate = _dbService.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EndDate;
            List<long> typeIDs = _dbService.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)).Select(s => s.EventTypeId).ToList();

            DateTime temp_Startdate = _dbService.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().StartDate;
            List <TimeZoneDTO> TZL = new List<TimeZoneDTO>();
            foreach (Models.Schedule item in _dbService.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)))
            {
                TimeZoneDTO TZ = new TimeZoneDTO();
                TZ.ScheduleId = item.ScheduleId;
                TZ.EventTypeId = item.EventTypeId;
                TZ.ParticipantId = item.ParticipantId;
                TZ.LocationId = item.LocationId;
                TZ.StartTime = Convert.ToInt32((item.StartTime - temp_Startdate.Date).TotalMinutes);
                TZ.EndTime = Convert.ToInt32((item.EndTime - temp_Startdate.Date).TotalMinutes);
                TZ.Slot = item.Slot;
                TZL.Add(TZ);
            }
            TempData.TimeZones = TZL;

            List<EventTypeDTO> ETL = new List<EventTypeDTO>();
            foreach (EventType item in _dbService.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)))
            {
                EventTypeDTO ET = new EventTypeDTO();
                ET.EventTypeId = item.EventTypeId.ToString();
                ET.TypeName = item.TypeName;
                ETL.Add(ET);
            }
            TempData.EventTypes = ETL;

            List<ParticipantDTO> SPL = new List<ParticipantDTO>();
            foreach (Participant item in _dbService.Participants.Where(e => e.EventId == Convert.ToInt32(id)))
            {
                ParticipantDTO SP = new ParticipantDTO();
                SP.ParticipantId = item.ParticipantId.ToString();
                SP.ParticipantName = item.ParticipantName;
                SPL.Add(SP);
            }
            TempData.Participants = SPL;

            List<LocationDTO> SLL = new List<LocationDTO>();
            foreach (Location item in _dbService.Locations.Where(e => e.EventId == Convert.ToInt32(id)))
            {
                LocationDTO SL = new LocationDTO();
                SL.LocationId = item.LocationId.ToString();
                SL.LocationName = item.LocationName;
                SLL.Add(SL);
            }
            TempData.Locations = SLL;

            List<ConstraintDTO> SCL = new List<ConstraintDTO>();
            foreach (Constraint item in _dbService.Constraints.Where(e => e.EventId == Convert.ToInt32(id)))
            {
                ConstraintDTO SC = new ConstraintDTO();
                SC.ConstraintId = item.ConstraintId.ToString();
                SC.ConstraintType = item.ConstraintType;
                SC.ObjectId = item.ObjectId.ToString();
                SC.StartTime = item.StartTime;
                SC.EndTime = item.EndTime;
                SCL.Add(SC);
            }
            TempData.Constraints = SCL;

            return TempData;
        }
        public async Task<ScheduleDataForEXPORT> GetScheduleDataEXPORT(string id)
        {
            ScheduleDataForEXPORT TempData = new ScheduleDataForEXPORT();
            TempData.EventName = _dbService.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EventName;
            TempData.StartDate = _dbService.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().StartDate;
            TempData.EndDate = _dbService.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EndDate;
            List<long> typeIDs = _dbService.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)).Select(s => s.EventTypeId).ToList();

            var EventTypes = _dbService.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)).ToList();
            var Participants = _dbService.Participants.Where(e => e.EventId == Convert.ToInt32(id)).ToList();
            var Locations = _dbService.Locations.Where(e => e.EventId == Convert.ToInt32(id)).ToList();
            var Groups = _dbService.Groups.Where(e => e.EventId == Convert.ToInt32(id)).ToList();

            List<ScheduleTimeZoneEXPORT> TZL = new List<ScheduleTimeZoneEXPORT>();
            foreach (Models.Schedule item in _dbService.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)))
            {
                ScheduleTimeZoneEXPORT TZ = new ScheduleTimeZoneEXPORT();
                TZ.EventType = EventTypes.Where(e => e.EventTypeId == item.EventTypeId).First().TypeName;
                TZ.Participant = Participants.Where(e => e.ParticipantId == item.ParticipantId).Select(s => s.ParticipantName + " (" + s.CompetitorNumber + ")").First();
                TZ.Location = Locations.Where(e => e.LocationId == item.LocationId).First().LocationName;
                TZ.StartTime = item.StartTime;
                TZ.EndTime = item.EndTime;
                TZ.Slot = item.Slot;

                var Participant = Participants.FirstOrDefault(p => p.ParticipantId == item.ParticipantId);
                var GroupId = Participant?.GroupId ?? -1;
                var GroupName = Groups.FirstOrDefault(g => g.GroupId == GroupId)?.GroupName ?? "";
                TZ.GroupName = GroupName;

                TZL.Add(TZ);
            }
            TempData.TimeZones = TZL;

            return TempData;
        }
    }
}
