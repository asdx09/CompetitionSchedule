using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Controllers;
using ScheduleLogic.Server.Models;
using System;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ScheduleLogic.Server.Services
{
    public class DatabaseService
    {
        private readonly ScheduleLogicDbContext _context;

        public DatabaseService(ScheduleLogicDbContext context)
        {
            _context = context;
        }

        public bool LoginUser(string username, string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null) return false;

            var passwordHasher = new PasswordHasher<User>();
            var result = passwordHasher.VerifyHashedPassword(user, user.Password, password);

            return result == PasswordVerificationResult.Success;
        }
        public string RegisterUser(string username, string password, string email)
        {
            if (_context.Users.Any(u => u.Username == username))
                return "Username already taken!";

            var passwordHasher = new PasswordHasher<User>();
            var user = new User
            {
                Username = username
            };
            user.Password = passwordHasher.HashPassword(user, password);
            user.Email = email;
            user.Validated = true;

            _context.Users.Add(user);
            _context.SaveChanges();
            return "";
        }
        public long NewEvent(string username)
        {
            Event NewEvent = new Event();
            NewEvent.CreatedBy = GetUserID(username);
            NewEvent.EventName = "New Event";
            NewEvent.StartDate = DateTime.Now;
            NewEvent.EndDate = DateTime.Now;
            NewEvent.BasePauseTime = 5;
            NewEvent.LocationPauseTime = 5;
            NewEvent.IsPrivate = true;
            NewEvent.LocWeight = 1;
            NewEvent.CompWeight = 1;
            NewEvent.GroupWeight = 1;
            NewEvent.TypeWeight = 1;
            var tempEvent = _context.Events.Add(NewEvent);
            _context.SaveChanges();
            return tempEvent.Entity.EventId;
        }
        public bool NewWizardEvent(dataDTO Data, string username)
        {
            using var tx = _context.Database.BeginTransaction();

            long id = NewEvent(username);

            _context.Events.Where(w => w.EventId == id).First().EventName = Data.eventData.EventName;
            _context.Events.Where(w => w.EventId == id).First().StartDate = Data.eventData.StartDate;
            _context.Events.Where(w => w.EventId == id).First().EndDate = Data.eventData.EndDate;
            _context.Events.Where(w => w.EventId == id).First().BasePauseTime = Data.eventData.BasePauseTime;
            _context.Events.Where(w => w.EventId == id).First().LocationPauseTime = Data.eventData.LocationPauseTime;

            try { _context.SaveChanges(); } catch { return false; }


            var eventTypeMap = new Dictionary<string, EventType>();
            foreach (EventTypeDTO item in Data.eventTypes)
            {
                EventType temp = new EventType();
                temp.EventId = id;
                temp.TypeName = item.TypeName;
                temp.TimeRange = item.TimeRange;
                _context.EventTypes.Add(temp);
                eventTypeMap[item.EventTypeId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var groupMap = new Dictionary<string, Group>();
            foreach (GroupDTO item in Data.groups)
            {
                Group temp = new Group();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.GroupName = item.GroupName;
                _context.Groups.Add(temp);
                groupMap[item.GroupId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var LocationMap = new Dictionary<string, Location>();
            foreach (LocationDTO item in Data.locations)
            {
                Location temp = new Location();
                temp.LocationName = item.LocationName;
                temp.EventId = (long)Convert.ToDouble(id);
                temp.Slots = item.Slots;
                _context.Locations.Add(temp);
                LocationMap[item.LocationId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var locationTableMap = new Dictionary<string, LocationTable>();
            foreach (LocationTableDTO item in Data.locationTable)
            {
                LocationTable temp = new LocationTable();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.LocationId = LocationMap[item.LocationId].LocationId;
                temp.EventTypeId = eventTypeMap[item.EventTypeId].EventTypeId;
                _context.LocationTables.Add(temp);
                locationTableMap[item.LocationTableId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var participantMap = new Dictionary<string, Participant>();
            foreach (ParticipantDTO item in Data.participants)
            {
                Participant temp = new Participant();
                temp.ParticipantName = item.ParticipantName;
                temp.EventId = (long)Convert.ToDouble(id);
                temp.CompetitorNumber = item.CompetitorNumber;
                if (item.GroupId != null && item.GroupId != "") temp.GroupId = groupMap[item.GroupId].GroupId;
                _context.Participants.Add(temp);
                participantMap[item.ParticipantId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var pauseTableMap = new Dictionary<string, PauseTable>();
            foreach (PauseTableDTO item in Data.pauseTable)
            {
                PauseTable temp = new PauseTable();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.LocationId1 = LocationMap[item.LocationId1].LocationId;
                temp.LocationId2 = LocationMap[item.LocationId2].LocationId;
                temp.Pause = item.Pause;
                _context.PauseTables.Add(temp);
                pauseTableMap[item.PauseId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var registrationMap = new Dictionary<string, Registration>();
            foreach (RegistrationDTO item in Data.registrations)
            {
                Registration temp = new Registration();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.ParticipantId = participantMap[item.ParticipantId].ParticipantId;
                temp.EventTypeId = eventTypeMap[item.EventTypeId].EventTypeId;
                _context.Registrations.Add(temp);
                registrationMap[item.RegistrationId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var ConstraintMap = new Dictionary<string, Constraint>();
            foreach (ConstraintDTO item in Data.constraints)
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
                            temp.ObjectId = groupMap[item.ObjectId].GroupId;
                            break;
                        case "C":
                            temp.ObjectId = participantMap[item.ObjectId].ParticipantId;
                            break;
                        case "T":
                            temp.ObjectId = eventTypeMap[item.ObjectId].EventTypeId;
                            break;
                    }
                }
                catch { return false; }
                temp.StartTime = item.StartTime;
                temp.EndTime = item.EndTime;
                _context.Constraints.Add(temp);
                ConstraintMap[item.ConstraintId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

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
        public void DeleteEvent(int id)
        {
            var eventToRemove = _context.Events.Where(w => w.EventId == id).ToList();
            _context.Events.RemoveRange(eventToRemove);
            var pauesToRemove = _context.PauseTables.Where(w => w.EventId == id).ToList();
            _context.PauseTables.RemoveRange(pauesToRemove);
            var locationTableToRemove = _context.LocationTables.Where(w => w.EventId == id).ToList();
            _context.LocationTables.RemoveRange(locationTableToRemove);
            var locationsToRemove = _context.Locations.Where(w => w.EventId == id).ToList();
            _context.Locations.RemoveRange(locationsToRemove);
            var schedulesToRemove = _context.Schedules.Where(w => w.EventTypeId == id).ToList();
            _context.Schedules.RemoveRange(schedulesToRemove);
            var registrationsToRemove = _context.Registrations.Where(w => w.EventId == id).ToList();
            _context.Registrations.RemoveRange(registrationsToRemove);
            var participantsToRemove = _context.Participants.Where(w => w.EventId == id).ToList();
            _context.Participants.RemoveRange(participantsToRemove);
            var groupsToRemove = _context.Groups.Where(w => w.EventId == id).ToList();
            _context.Groups.RemoveRange(groupsToRemove);
            var eventTypesToRemove = _context.EventTypes.Where(w => w.EventId == id).ToList();
            _context.EventTypes.RemoveRange(eventTypesToRemove);
            var constraintsToRemove = _context.Constraints.Where(w => w.EventId == id).ToList();
            _context.Constraints.RemoveRange(constraintsToRemove);
            _context.SaveChanges();
        }
        public long GetUserID(string username)
        {
            if (_context.Users.Count() < 1) return 0;
            return _context.Users.Where(w => w.Username == username).First().UserId;
        }
        public bool CheckUser(string username, string id)
        {
            return _context.Events.Where(w => w.CreatedBy == GetUserID(username) && w.EventId == Convert.ToInt32(id)).Count() > 0;
        }
        public bool CheckUserExist(string username)
        {
            return _context.Users.Where(w => w.Username == username).Count() > 0;
        }
        public List<EventsData> GetEvents(string username)
        {
            return _context.Events.Where(e => e.CreatedBy == GetUserID(username)).Select(e => new EventsData{event_ID = e.EventId, eventName = e.EventName}).ToList();
        }
        public dataDTO GetEvent(string id, string username)
        {
            dataDTO TempData = new dataDTO();
            TempData.eventData = _context.Events.AsNoTracking().Where(w => w.CreatedBy == GetUserID(username) && w.EventId == Convert.ToInt32(id)).Select(e => new EventDTO
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
            }).First();
            TempData.eventTypes = _context.EventTypes.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new EventTypeDTO
            {
                EventId = e.EventId.ToString(),
                EventTypeId = e.EventTypeId.ToString(),
                TypeName = e.TypeName,
                TimeRange = e.TimeRange
            }).ToList();
            TempData.groups = _context.Groups.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new GroupDTO
            {
                GroupId = e.GroupId.ToString(),
                EventId = e.EventId.ToString(),
                GroupName = e.GroupName
            }).ToList();
            TempData.locations = _context.Locations.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new LocationDTO
            {
                LocationId = e.LocationId.ToString(),
                EventId = e.EventId.ToString(),
                LocationName = e.LocationName,
                Slots = e.Slots
            }).ToList();
            TempData.participants = _context.Participants.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new ParticipantDTO
            {
                ParticipantId = e.ParticipantId.ToString(),
                ParticipantName = e.ParticipantName,
                CompetitorNumber = e.CompetitorNumber,
                EventId = e.EventId.ToString(),
                GroupId = e.GroupId.ToString()
            }).ToList();
            TempData.registrations = _context.Registrations.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new RegistrationDTO
            {
                RegistrationId = e.RegistrationId.ToString(),
                EventId= e.EventId.ToString(),
                ParticipantId= e.ParticipantId.ToString(),
                EventTypeId = e.EventTypeId.ToString()
            }).ToList();
            TempData.pauseTable = _context.PauseTables.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new PauseTableDTO
            {
                PauseId = e.PauseId.ToString(),
                EventId = e.EventId.ToString(),
                LocationId1 = e.LocationId1.ToString(),
                LocationId2 = e.LocationId2.ToString(),
                Pause = e.Pause
            }).ToList();
            TempData.locationTable = _context.LocationTables.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new LocationTableDTO
            {
                LocationTableId = e.LocationTableId.ToString(),
                EventId = e.EventId.ToString(),
                EventTypeId = e.EventTypeId.ToString(),
                LocationId = e.LocationId.ToString()
            }).ToList();
            TempData.constraints = _context.Constraints.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new ConstraintDTO
            {
                ConstraintId = e.ConstraintId.ToString(),
                EventId = e.EventId.ToString(),
                ObjectId = e.ObjectId.ToString(),
                ConstraintType = e.ConstraintType.ToString(),
                StartTime = e.StartTime,
                EndTime = e.EndTime
            }).ToList();
            return TempData;
        }
        public bool SaveEvent(dataDTO Data)
        {
            using var tx = _context.Database.BeginTransaction();

            long id = (long)Convert.ToDouble(Data.eventData.EventId);
            _context.Events.Where(w => w.EventId.ToString() == Data.eventData.EventId).First().EventName = Data.eventData.EventName;
            _context.Events.Where(w => w.EventId.ToString() == Data.eventData.EventId).First().StartDate = Data.eventData.StartDate;
            _context.Events.Where(w => w.EventId.ToString() == Data.eventData.EventId).First().EndDate = Data.eventData.EndDate;
            _context.Events.Where(w => w.EventId.ToString() == Data.eventData.EventId).First().IsPrivate = Data.eventData.IsPrivate;
            _context.Events.Where(w => w.EventId.ToString() == Data.eventData.EventId).First().BasePauseTime = Data.eventData.BasePauseTime;
            _context.Events.Where(w => w.EventId.ToString() == Data.eventData.EventId).First().LocationPauseTime = Data.eventData.LocationPauseTime;
            _context.Events.Where(w => w.EventId.ToString() == Data.eventData.EventId).First().LocWeight = Data.eventData.LocWeight;
            _context.Events.Where(w => w.EventId.ToString() == Data.eventData.EventId).First().GroupWeight = Data.eventData.GroupWeight;
            _context.Events.Where(w => w.EventId.ToString() == Data.eventData.EventId).First().TypeWeight = Data.eventData.TypeWeight;
            _context.Events.Where(w => w.EventId.ToString() == Data.eventData.EventId).First().CompWeight = Data.eventData.CompWeight;

            var eventTypesToRemove = _context.EventTypes.Where(w => w.EventId == id).ToList();
            _context.EventTypes.RemoveRange(eventTypesToRemove);
            var groupsToRemove = _context.Groups.Where(w => w.EventId == id).ToList();
            _context.Groups.RemoveRange(groupsToRemove);
            var locationsToRemove = _context.Locations.Where(w => w.EventId == id).ToList();
            _context.Locations.RemoveRange(locationsToRemove);
            var LocationTableToRemove = _context.LocationTables.Where(w => w.EventId == id).ToList();
            _context.LocationTables.RemoveRange(LocationTableToRemove);
            var ParticipantsToRemove = _context.Participants.Where(w => w.EventId == id).ToList();
            _context.Participants.RemoveRange(ParticipantsToRemove);
            var PauseTableToRemove = _context.PauseTables.Where(w => w.EventId == id).ToList();
            _context.PauseTables.RemoveRange(PauseTableToRemove);
            var RegistrationToRemove = _context.Registrations.Where(w => w.EventId == id).ToList();
            _context.Registrations.RemoveRange(RegistrationToRemove);
            var ConstraintsToRemove = _context.Constraints.Where(w => w.EventId == id).ToList();
            _context.Constraints.RemoveRange(ConstraintsToRemove);
            try { _context.SaveChanges(); } catch { return false; }


            var eventTypeMap = new Dictionary<string, EventType>();
            foreach (EventTypeDTO item in Data.eventTypes)
            {
                EventType temp = new EventType();
                temp.EventId = id;
                temp.TypeName = item.TypeName;
                temp.TimeRange = item.TimeRange;
                _context.EventTypes.Add(temp);
                eventTypeMap[item.EventTypeId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var groupMap = new Dictionary<string, Group>();
            foreach (GroupDTO item in Data.groups)
            {
                Group temp = new Group();
                temp.EventId = (long)Convert.ToDouble(Data.eventData.EventId);
                temp.GroupName = item.GroupName;
                _context.Groups.Add(temp);
                groupMap[item.GroupId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var LocationMap = new Dictionary<string, Location>();
            foreach (LocationDTO item in Data.locations)
            {
                Location temp = new Location();
                temp.LocationName = item.LocationName;
                temp.EventId = (long)Convert.ToDouble(Data.eventData.EventId);
                temp.Slots = item.Slots;
                _context.Locations.Add(temp);
                LocationMap[item.LocationId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var locationTableMap = new Dictionary<string, LocationTable>();
            foreach (LocationTableDTO item in Data.locationTable)
            {
                LocationTable temp = new LocationTable();
                temp.EventId = (long)Convert.ToDouble(Data.eventData.EventId);
                temp.LocationId = LocationMap[item.LocationId].LocationId;
                temp.EventTypeId = eventTypeMap[item.EventTypeId].EventTypeId;
                _context.LocationTables.Add(temp);
                locationTableMap[item.LocationTableId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var participantMap = new Dictionary<string, Participant>();
            foreach (ParticipantDTO item in Data.participants)
            {
                Participant temp = new Participant();
                temp.ParticipantName = item.ParticipantName;
                temp.EventId = (long)Convert.ToDouble(Data.eventData.EventId);
                temp.CompetitorNumber = item.CompetitorNumber;
                if (item.GroupId != null && item.GroupId != "") temp.GroupId = groupMap[item.GroupId].GroupId;
                _context.Participants.Add(temp);
                participantMap[item.ParticipantId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var pauseTableMap = new Dictionary<string, PauseTable>();
            foreach (PauseTableDTO item in Data.pauseTable)
            {
                PauseTable temp = new PauseTable();
                temp.EventId = (long)Convert.ToDouble(Data.eventData.EventId);
                temp.LocationId1 = LocationMap[item.LocationId1].LocationId;
                temp.LocationId2 = LocationMap[item.LocationId2].LocationId;
                temp.Pause = item.Pause;
                _context.PauseTables.Add(temp);
                pauseTableMap[item.PauseId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var registrationMap = new Dictionary<string, Registration>();
            foreach (RegistrationDTO item in Data.registrations)
            {
                Registration temp = new Registration();
                temp.EventId = (long)Convert.ToDouble(Data.eventData.EventId);
                temp.ParticipantId = participantMap[item.ParticipantId].ParticipantId;
                temp.EventTypeId = eventTypeMap[item.EventTypeId].EventTypeId;
                _context.Registrations.Add(temp);
                registrationMap[item.RegistrationId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var ConstraintMap = new Dictionary<string, Constraint>();
            foreach (ConstraintDTO item in Data.constraints)
            {
                Constraint temp = new Constraint();
                temp.EventId = (long)Convert.ToDouble(Data.eventData.EventId);
                temp.ConstraintType = item.ConstraintType;
                try
                {
                    switch (temp.ConstraintType)
                    {
                        case "L":
                            temp.ObjectId = LocationMap[item.ObjectId].LocationId;
                            break;
                        case "G":
                            temp.ObjectId = groupMap[item.ObjectId].GroupId;
                            break;
                        case "C":
                            temp.ObjectId = participantMap[item.ObjectId].ParticipantId;
                            break;
                        case "T":
                            temp.ObjectId = eventTypeMap[item.ObjectId].EventTypeId;
                            break;
                    }
                } catch { return false; }
                temp.StartTime = item.StartTime;
                temp.EndTime = item.EndTime;
                _context.Constraints.Add(temp);
                ConstraintMap[item.ConstraintId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

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
        public ScheduleRequestForSolver GetScheduleInfo(int id)
        {
            ScheduleRequestForSolver request = new ScheduleRequestForSolver();

            request.event_id = id;

            List<LocationModel> LML = new List<LocationModel>();
            foreach (Location item in _context.Locations.Where(w => w.EventId == id))
            {
                LocationModel LM = new LocationModel();
                LM.id = item.LocationId;
                LM.name = item.LocationName;
                LM.capacity = Convert.ToInt32(item.Slots);
                LML.Add(LM);
            }
            request.locations = LML;

            var EML = _context.EventTypes.Where(e => e.EventId == id).Select(e => new EventModel
            {
                id = e.EventTypeId,
                name = e.TypeName,
                duration = e.TimeRange.Hour * 60 + e.TimeRange.Minute,
                possible_locations = _context.LocationTables
                    .Where(l => l.EventTypeId == e.EventTypeId)
                    .Select(l => l.LocationId)
                    .ToList()
            }).ToList();
            request.events = EML;

            List<CompetitorModel> CML = new List<CompetitorModel>();
            foreach (Participant item in _context.Participants.Where(w => w.EventId == id))
            {
                CompetitorModel CM = new CompetitorModel();
                CM.id = item.ParticipantId;
                CM.name = item.ParticipantName;
                CM.group_id = item.GroupId ?? -1;
                CML.Add(CM);
            }
            request.competitors = CML;

            List<EntryModel> EnML = new List<EntryModel>();
            foreach (Registration item in _context.Registrations.Where(w => w.EventId == id))
            {
                EntryModel EnM = new EntryModel();
                EnM.id = item.RegistrationId;
                EnM.event_id = item.EventTypeId;
                EnM.competitor_id = item.ParticipantId;
                EnML.Add(EnM);
            }
            request.entries = EnML;

            var eventStart = _context.Events.Where(w => w.EventId == id).Select(s => s.StartDate).First();
            List<ConstraintModel> CoML = new List<ConstraintModel>();
            foreach (Constraint item in _context.Constraints.Where(w => w.EventId == id))
            {
                ConstraintModel CoM = new ConstraintModel();
                CoM.id = item.ConstraintId;
                CoM.ConstraintType = item.ConstraintType;
                CoM.object_id = item.ObjectId;
                CoM.StartTime = (int)(item.StartTime - eventStart).TotalMinutes;
                CoM.EndTime = (int)(item.EndTime - eventStart).TotalMinutes;
                CoML.Add(CoM);
            }
            request.constraints = CoML;


            List<PauseTableModel> PL = new List<PauseTableModel>();
            foreach (PauseTable item in _context.PauseTables.Where(w => w.EventId == id))
            {
                PauseTableModel PM = new PauseTableModel();
                PM.id = item.PauseId;
                PM.LocationId1 = item.LocationId1;
                PM.LocationId2 = item.LocationId2;
                PM.Pause = item.Pause.Hour * 60 + item.Pause.Minute;
                PL.Add(PM);
            }
            request.travel = PL;

            request.day_length = 24 * 60 - 1;
            request.max_days = (_context.Events.Where(w => w.EventId == id).First().EndDate - _context.Events.Where(w => w.EventId == id).FirstOrDefault().StartDate).Days + 1;
            request.break_time_loc = _context.Events.Where(w => w.EventId == id).First().LocationPauseTime;
            request.base_pause_time = _context.Events.Where(w => w.EventId == id).First().BasePauseTime;
            request.locWeight = Math.Min(500,_context.Events.Where(w => w.EventId == id).First().LocWeight);
            request.groupWeight = Math.Min(500, _context.Events.Where(w => w.EventId == id).First().GroupWeight);
            request.typeWeight = Math.Min(500, _context.Events.Where(w => w.EventId == id).First().TypeWeight);
            request.compWeight = Math.Min(500, _context.Events.Where(w => w.EventId == id).First().CompWeight);
            return request;
        }
        public bool NewSchedule(List<Schedule> request, int event_id)
        {
            List<long> typeIDs = _context.EventTypes.Where(w => w.EventId == event_id).Select(s => s.EventTypeId).ToList();
            DateTime eventstart = _context.Events.Where(w => w.EventId == event_id).Select(s => s.StartDate).First();
            var itemsToRemove = _context.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)).ToList();
            _context.Schedules.RemoveRange(itemsToRemove);

            foreach (var item in request)
            {
                Models.Schedule NS = new Models.Schedule();
                NS.ParticipantId = item.participant_id;
                NS.StartTime = eventstart.AddMinutes(item.start);
                NS.EndTime = eventstart.AddMinutes(item.end);
                NS.LocationId = item.location_id;
                NS.Slot = item.slot;
                NS.EventTypeId = item.eventtype_id;
                _context.Schedules.Add(NS);
            }

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                return false;
            }

            var schedules = _context.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)).ToList();
            var pause = _context.Events.Where(w => w.EventId == event_id).First().LocationPauseTime;
            var locations = _context.Locations.ToList();

            var groupedByLocation = schedules.GroupBy(s => s.LocationId);

            foreach (var group in groupedByLocation)
            {
                var entries = group.OrderBy(s => s.StartTime).ToList();
                var activeSlots = new List<Models.Schedule>();

                int max = locations
                    .First(l => l.LocationId == group.Key)
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
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }
        public ScheduleDataForFrontEnd GetScheduleData(string id)
        {
            ScheduleDataForFrontEnd TempData = new ScheduleDataForFrontEnd();
            TempData.event_ID = Convert.ToInt32(id);
            TempData.eventName = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EventName;
            TempData.startDate = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().StartDate;
            TempData.endDate = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EndDate;
            List<long> typeIDs = _context.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)).Select(s => s.EventTypeId).ToList();

            DateTime temp_startdate = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().StartDate;
            List <scheduleTimeZone> TZL = new List<scheduleTimeZone>();
            foreach (Models.Schedule item in _context.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)))
            {
                scheduleTimeZone TZ = new scheduleTimeZone();
                TZ.schedule_ID = item.ScheduleId;
                TZ.eventType_ID = item.EventTypeId;
                TZ.participant_ID = item.ParticipantId;
                TZ.location_ID = item.LocationId;
                TZ.StartTime = Convert.ToInt32((item.StartTime - temp_startdate.Date).TotalMinutes);
                TZ.EndTime = Convert.ToInt32((item.EndTime - temp_startdate.Date).TotalMinutes);
                TZ.Slot = item.Slot;
                TZL.Add(TZ);
            }
            TempData.timeZones = TZL;

            List<scheduleEventType> ETL = new List<scheduleEventType>();
            foreach (EventType item in _context.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)))
            {
                scheduleEventType ET = new scheduleEventType();
                ET.eventType_ID = item.EventTypeId;
                ET.eventTypeName = item.TypeName;
                ETL.Add(ET);
            }
            TempData.eventTypes = ETL;

            List<scheduleParticipans> SPL = new List<scheduleParticipans>();
            foreach (Participant item in _context.Participants.Where(e => e.EventId == Convert.ToInt32(id)))
            {
                scheduleParticipans SP = new scheduleParticipans();
                SP.participant_ID = item.ParticipantId;
                SP.participantName = item.ParticipantName;
                SPL.Add(SP);
            }
            TempData.participans = SPL;

            List<scheduleLocations> SLL = new List<scheduleLocations>();
            foreach (Location item in _context.Locations.Where(e => e.EventId == Convert.ToInt32(id)))
            {
                scheduleLocations SL = new scheduleLocations();
                SL.location_ID = item.LocationId;
                SL.locationName = item.LocationName;
                SLL.Add(SL);
            }
            TempData.locations = SLL;

            List<scheduleConstraint> SCL = new List<scheduleConstraint>();
            foreach (Constraint item in _context.Constraints.Where(e => e.EventId == Convert.ToInt32(id)))
            {
                scheduleConstraint SC = new scheduleConstraint();
                SC.id = item.ConstraintId;
                SC.ConstraintType = item.ConstraintType;
                SC.object_ID = item.ObjectId;
                SC.StartTime = Convert.ToInt32((item.StartTime - temp_startdate.Date).TotalMinutes);
                SC.EndTime = Convert.ToInt32((item.EndTime - temp_startdate.Date).TotalMinutes);
                SCL.Add(SC);
            }
            TempData.constraints = SCL;

            return TempData;
        }

        public ScheduleDataForEXPORT GetScheduleDataEXPORT(string id)
        {
            ScheduleDataForEXPORT TempData = new ScheduleDataForEXPORT();
            TempData.eventName = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EventName;
            TempData.startDate = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().StartDate;
            TempData.endDate = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EndDate;
            List<long> typeIDs = _context.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)).Select(s => s.EventTypeId).ToList();

            var eventtypes = _context.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)).ToList();
            var participants = _context.Participants.Where(e => e.EventId == Convert.ToInt32(id)).ToList();
            var locations = _context.Locations.Where(e => e.EventId == Convert.ToInt32(id)).ToList();
            var groups = _context.Groups.Where(e => e.EventId == Convert.ToInt32(id)).ToList();

            List<scheduleTimeZoneEXPORT> TZL = new List<scheduleTimeZoneEXPORT>();
            foreach (Models.Schedule item in _context.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)))
            {
                scheduleTimeZoneEXPORT TZ = new scheduleTimeZoneEXPORT();
                TZ.eventType = eventtypes.Where(e => e.EventTypeId == item.EventTypeId).First().TypeName;
                TZ.participant = participants.Where(e => e.ParticipantId == item.ParticipantId).Select(s => s.ParticipantName + " (" + s.CompetitorNumber + ")").First();
                TZ.location = locations.Where(e => e.LocationId == item.LocationId).First().LocationName;
                TZ.StartTime = item.StartTime;
                TZ.EndTime = item.EndTime;
                TZ.Slot = item.Slot;

                var participant = participants.FirstOrDefault(p => p.ParticipantId == item.ParticipantId);
                var groupId = participant?.GroupId ?? -1;
                var groupName = groups.FirstOrDefault(g => g.GroupId == groupId)?.GroupName ?? "";
                TZ.groupName = groupName;

                TZL.Add(TZ);
            }
            TempData.timeZones = TZL;

            return TempData;
        }
    }
}
