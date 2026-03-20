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

            var evt = await _dbService.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventId == id);

            // Locations
            request.Locations = await _dbService.Locations
                .Where(l => l.EventId == id)
                .Select(l => new LocationModel
                {
                    Id = l.LocationId,
                    Name = l.LocationName,
                    Capacity = (int)l.Slots
                })
                .ToListAsync();

            // EventTypes + PossibleLocations
            var eventTypes = await _dbService.EventTypes
                .Where(e => e.EventId == id)
                .ToListAsync();

            var locationTables = await _dbService.LocationTables
                .Where(lt => eventTypes.Select(e => e.EventTypeId).Contains(lt.EventTypeId))
                .ToListAsync();

            request.Events = eventTypes.Select(e => new EventModel
            {
                Id = e.EventTypeId,
                Name = e.TypeName,
                Duration = e.TimeRange.Hour * 60 + e.TimeRange.Minute,
                PossibleLocations = locationTables
                    .Where(lt => lt.EventTypeId == e.EventTypeId)
                    .Select(lt => lt.LocationId)
                    .ToList()
            }).ToList();

            // Participants
            request.Competitors = await _dbService.Participants
                .Where(p => p.EventId == id)
                .Select(p => new CompetitorModel
                {
                    Id = p.ParticipantId,
                    Name = p.ParticipantName,
                    GroupId = p.GroupId ?? -1
                })
                .ToListAsync();

            // Registrations
            request.Entries = await _dbService.Registrations
                .Where(r => r.EventId == id)
                .Select(r => new EntryModel
                {
                    Id = r.RegistrationId,
                    EventId = r.EventTypeId,
                    CompetitorId = r.ParticipantId
                })
                .ToListAsync();

            // Constraints
            var constraints = await _dbService.Constraints
                .Where(c => c.EventId == id)
                .ToListAsync();

            request.Constraints = constraints.Select(c => new ConstraintModel
            {
                Id = c.ConstraintId,
                ConstraintType = c.ConstraintType,
                ObjectId = c.ObjectId,
                StartTime = (int)(c.StartTime - evt.StartDate).TotalMinutes,
                EndTime = (int)(c.EndTime - evt.StartDate).TotalMinutes
            }).ToList();

            // PauseTable / Travel
            request.Travel = await _dbService.PauseTables
                .Where(p => p.EventId == id)
                .Select(p => new PauseTableModel
                {
                    Id = p.PauseId,
                    LocationID1 = p.LocationId1,
                    LocationID2 = p.LocationId2,
                    Pause = p.Pause.Hour * 60 + p.Pause.Minute
                })
                .ToListAsync();

            // Event info
            request.DayLength = 24 * 60 - 1;
            request.MaxDays = (evt.EndDate - evt.StartDate).Days + 1;
            request.BreakTimeLoc = evt.LocationPauseTime;
            request.BasePauseTime = evt.BasePauseTime;
            request.LocWeight = Math.Min(500, evt.LocWeight);
            request.GroupWeight = Math.Min(500, evt.GroupWeight);
            request.TypeWeight = Math.Min(500, evt.TypeWeight);
            request.CompWeight = Math.Min(500, evt.CompWeight);

            return request;
        }
        public async Task<bool> NewSchedule(List<ScheduleModel> request, int Event_id)
        {
            List<long> typeIDs = await _dbService.EventTypes.Where(w => w.EventId == Event_id).Select(s => s.EventTypeId).ToListAsync();
            DateTime EventStart = _dbService.Events.Where(w => w.EventId == Event_id).Select(s => s.StartDate).First();
            var itemsToRemove = await _dbService.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)).ToListAsync();
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

            await _dbService.SaveChangesAsync();

            var Schedules = await _dbService.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)).ToListAsync();
            var pause = _dbService.Events.Where(w => w.EventId == Event_id).First().LocationPauseTime;
            var Locations = await _dbService.Locations.ToListAsync();

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


            await _dbService.SaveChangesAsync();
            return true;
        }
        public async Task<DataDTO> GetScheduleData(string id)
        {
            int eventId = Convert.ToInt32(id);
            var tempData = new DataDTO();
            tempData.EventData.EventId = id;

            var evt = await _dbService.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (evt == null) return null;

            tempData.EventData.EventName = evt.EventName;
            tempData.EventData.StartDate = evt.StartDate;
            tempData.EventData.EndDate = evt.EndDate;

            var typeIDs = await _dbService.EventTypes
                .Where(e => e.EventId == eventId)
                .Select(e => e.EventTypeId)
                .ToListAsync();

            var schedules = await _dbService.Schedules
                .Where(s => typeIDs.Contains(s.EventTypeId))
                .ToListAsync();

            tempData.TimeZones = schedules.Select(item => new TimeZoneDTO
            {
                ScheduleId = item.ScheduleId,
                EventTypeId = item.EventTypeId,
                ParticipantId = item.ParticipantId,
                LocationId = item.LocationId,
                StartTime = Convert.ToInt32((item.StartTime - evt.StartDate.Date).TotalMinutes),
                EndTime = Convert.ToInt32((item.EndTime - evt.StartDate.Date).TotalMinutes),
                Slot = item.Slot
            }).ToList();

            tempData.EventTypes = await _dbService.EventTypes
                .Where(e => e.EventId == eventId)
                .Select(e => new EventTypeDTO
                {
                    EventTypeId = e.EventTypeId.ToString(),
                    TypeName = e.TypeName
                })
                .ToListAsync();

            tempData.Participants = await _dbService.Participants
                .Where(p => p.EventId == eventId)
                .Select(p => new ParticipantDTO
                {
                    ParticipantId = p.ParticipantId.ToString(),
                    ParticipantName = p.ParticipantName
                })
                .ToListAsync();

            tempData.Locations = await _dbService.Locations
                .Where(l => l.EventId == eventId)
                .Select(l => new LocationDTO
                {
                    LocationId = l.LocationId.ToString(),
                    LocationName = l.LocationName
                })
                .ToListAsync();

            tempData.Constraints = await _dbService.Constraints
                .Where(c => c.EventId == eventId)
                .Select(c => new ConstraintDTO
                {
                    ConstraintId = c.ConstraintId.ToString(),
                    ConstraintType = c.ConstraintType,
                    ObjectId = c.ObjectId.ToString(),
                    StartTime = c.StartTime,
                    EndTime = c.EndTime
                })
                .ToListAsync();

            return tempData;
        }
        public async Task<ScheduleDataForEXPORT> GetScheduleDataEXPORT(string id)
        {
            int eventId = Convert.ToInt32(id);

            var evt = await _dbService.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (evt == null) return null;

            var typeIDs = await _dbService.EventTypes
                .Where(e => e.EventId == eventId)
                .Select(e => e.EventTypeId)
                .ToListAsync();

            var eventTypes = await _dbService.EventTypes
                .Where(e => e.EventId == eventId)
                .ToListAsync();

            var participants = await _dbService.Participants
                .Where(p => p.EventId == eventId)
                .ToListAsync();

            var locations = await _dbService.Locations
                .Where(l => l.EventId == eventId)
                .ToListAsync();

            var groups = await _dbService.Groups
                .Where(g => g.EventId == eventId)
                .ToListAsync();

            var schedules = await _dbService.Schedules
                .Where(s => typeIDs.Contains(s.EventTypeId))
                .ToListAsync();

            var eventTypeDict = eventTypes.ToDictionary(e => e.EventTypeId, e => e.TypeName);
            var participantDict = participants.ToDictionary(p => p.ParticipantId, p => p);
            var locationDict = locations.ToDictionary(l => l.LocationId, l => l.LocationName);
            var groupDict = groups.ToDictionary(g => g.GroupId, g => g.GroupName);

            var timeZones = schedules.Select(item =>
            {
                var participant = participantDict[item.ParticipantId];
                var groupName = participant.GroupId.HasValue && groupDict.ContainsKey(participant.GroupId.Value)
                    ? groupDict[participant.GroupId.Value]
                    : "";

                return new ScheduleTimeZoneEXPORT
                {
                    EventType = eventTypeDict[item.EventTypeId],
                    Participant = $"{participant.ParticipantName} ({participant.CompetitorNumber})",
                    Location = locationDict[item.LocationId],
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    Slot = item.Slot,
                    GroupName = groupName
                };
            }).ToList();

            return new ScheduleDataForEXPORT
            {
                EventName = evt.EventName,
                StartDate = evt.StartDate,
                EndDate = evt.EndDate,
                TimeZones = timeZones
            };
        }
    }
}
