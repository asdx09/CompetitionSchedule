using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Controllers;
using ScheduleLogic.Server.Models;
using System;
using System.Linq;
using static ScheduleLogic.Server.Class.EventModels;
using static ScheduleLogic.Server.Class.ScheduleModels;
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
        public bool NewWizardEvent(DataDTO Data, string username)
        {
            using var tx = _context.Database.BeginTransaction();

            long id = NewEvent(username);

            _context.Events.Where(w => w.EventId == id).First().EventName = Data.EventData.EventName;
            _context.Events.Where(w => w.EventId == id).First().StartDate = Data.EventData.StartDate;
            _context.Events.Where(w => w.EventId == id).First().EndDate = Data.EventData.EndDate;
            _context.Events.Where(w => w.EventId == id).First().BasePauseTime = Data.EventData.BasePauseTime;
            _context.Events.Where(w => w.EventId == id).First().LocationPauseTime = Data.EventData.LocationPauseTime;

            try { _context.SaveChanges(); } catch { return false; }


            var EventTypeMap = new Dictionary<string, EventType>();
            foreach (EventTypeDTO item in Data.EventTypes)
            {
                EventType temp = new EventType();
                temp.EventId = id;
                temp.TypeName = item.TypeName;
                temp.TimeRange = item.TimeRange;
                _context.EventTypes.Add(temp);
                EventTypeMap[item.EventTypeId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var GroupMap = new Dictionary<string, Group>();
            foreach (GroupDTO item in Data.Groups)
            {
                Group temp = new Group();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.GroupName = item.GroupName;
                _context.Groups.Add(temp);
                GroupMap[item.GroupId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var LocationMap = new Dictionary<string, Location>();
            foreach (LocationDTO item in Data.Locations)
            {
                Location temp = new Location();
                temp.LocationName = item.LocationName;
                temp.EventId = (long)Convert.ToDouble(id);
                temp.Slots = item.Slots;
                _context.Locations.Add(temp);
                LocationMap[item.LocationId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var LocationTableMap = new Dictionary<string, LocationTable>();
            foreach (LocationTableDTO item in Data.LocationTable)
            {
                LocationTable temp = new LocationTable();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.LocationId = LocationMap[item.LocationId].LocationId;
                temp.EventTypeId = EventTypeMap[item.EventTypeId].EventTypeId;
                _context.LocationTables.Add(temp);
                LocationTableMap[item.LocationTableId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var ParticipantMap = new Dictionary<string, Participant>();
            foreach (ParticipantDTO item in Data.Participants)
            {
                Participant temp = new Participant();
                temp.ParticipantName = item.ParticipantName;
                temp.EventId = (long)Convert.ToDouble(id);
                temp.CompetitorNumber = item.CompetitorNumber;
                if (item.GroupId != null && item.GroupId != "") temp.GroupId = GroupMap[item.GroupId].GroupId;
                _context.Participants.Add(temp);
                ParticipantMap[item.ParticipantId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var pauseTableMap = new Dictionary<string, PauseTable>();
            foreach (PauseTableDTO item in Data.PauseTable)
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
            foreach (RegistrationDTO item in Data.Registrations)
            {
                Registration temp = new Registration();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.ParticipantId = ParticipantMap[item.ParticipantId].ParticipantId;
                temp.EventTypeId = EventTypeMap[item.EventTypeId].EventTypeId;
                _context.Registrations.Add(temp);
                registrationMap[item.RegistrationId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

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
            var EventToRemove = _context.Events.Where(w => w.EventId == id).ToList();
            _context.Events.RemoveRange(EventToRemove);
            var pauesToRemove = _context.PauseTables.Where(w => w.EventId == id).ToList();
            _context.PauseTables.RemoveRange(pauesToRemove);
            var LocationTableToRemove = _context.LocationTables.Where(w => w.EventId == id).ToList();
            _context.LocationTables.RemoveRange(LocationTableToRemove);
            var LocationsToRemove = _context.Locations.Where(w => w.EventId == id).ToList();
            _context.Locations.RemoveRange(LocationsToRemove);
            var SchedulesToRemove = _context.Schedules.Where(w => w.EventTypeId == id).ToList();
            _context.Schedules.RemoveRange(SchedulesToRemove);
            var registrationsToRemove = _context.Registrations.Where(w => w.EventId == id).ToList();
            _context.Registrations.RemoveRange(registrationsToRemove);
            var ParticipantsToRemove = _context.Participants.Where(w => w.EventId == id).ToList();
            _context.Participants.RemoveRange(ParticipantsToRemove);
            var GroupsToRemove = _context.Groups.Where(w => w.EventId == id).ToList();
            _context.Groups.RemoveRange(GroupsToRemove);
            var EventTypesToRemove = _context.EventTypes.Where(w => w.EventId == id).ToList();
            _context.EventTypes.RemoveRange(EventTypesToRemove);
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
            return _context.Events.Where(e => e.CreatedBy == GetUserID(username)).Select(e => new EventsData{EventId = e.EventId, EventName = e.EventName}).ToList();
        }
        public DataDTO GetEvent(string id, string username)
        {
            DataDTO TempData = new DataDTO();
            TempData.EventData = _context.Events.AsNoTracking().Where(w => w.CreatedBy == GetUserID(username) && w.EventId == Convert.ToInt32(id)).Select(e => new EventDTO
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
            TempData.EventTypes = _context.EventTypes.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new EventTypeDTO
            {
                EventId = e.EventId.ToString(),
                EventTypeId = e.EventTypeId.ToString(),
                TypeName = e.TypeName,
                TimeRange = e.TimeRange
            }).ToList();
            TempData.Groups = _context.Groups.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new GroupDTO
            {
                GroupId = e.GroupId.ToString(),
                EventId = e.EventId.ToString(),
                GroupName = e.GroupName
            }).ToList();
            TempData.Locations = _context.Locations.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new LocationDTO
            {
                LocationId = e.LocationId.ToString(),
                EventId = e.EventId.ToString(),
                LocationName = e.LocationName,
                Slots = e.Slots
            }).ToList();
            TempData.Participants = _context.Participants.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new ParticipantDTO
            {
                ParticipantId = e.ParticipantId.ToString(),
                ParticipantName = e.ParticipantName,
                CompetitorNumber = e.CompetitorNumber,
                EventId = e.EventId.ToString(),
                GroupId = e.GroupId.ToString()
            }).ToList();
            TempData.Registrations = _context.Registrations.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new RegistrationDTO
            {
                RegistrationId = e.RegistrationId.ToString(),
                EventId= e.EventId.ToString(),
                ParticipantId= e.ParticipantId.ToString(),
                EventTypeId = e.EventTypeId.ToString()
            }).ToList();
            TempData.PauseTable = _context.PauseTables.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new PauseTableDTO
            {
                PauseId = e.PauseId.ToString(),
                EventId = e.EventId.ToString(),
                LocationId1 = e.LocationId1.ToString(),
                LocationId2 = e.LocationId2.ToString(),
                Pause = e.Pause
            }).ToList();
            TempData.LocationTable = _context.LocationTables.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new LocationTableDTO
            {
                LocationTableId = e.LocationTableId.ToString(),
                EventId = e.EventId.ToString(),
                EventTypeId = e.EventTypeId.ToString(),
                LocationId = e.LocationId.ToString()
            }).ToList();
            TempData.Constraints = _context.Constraints.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new ConstraintDTO
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
        public bool SaveEvent(DataDTO Data)
        {
            using var tx = _context.Database.BeginTransaction();

            long id = (long)Convert.ToDouble(Data.EventData.EventId);
            _context.Events.Where(w => w.EventId.ToString() == Data.EventData.EventId).First().EventName = Data.EventData.EventName;
            _context.Events.Where(w => w.EventId.ToString() == Data.EventData.EventId).First().StartDate = Data.EventData.StartDate;
            _context.Events.Where(w => w.EventId.ToString() == Data.EventData.EventId).First().EndDate = Data.EventData.EndDate;
            _context.Events.Where(w => w.EventId.ToString() == Data.EventData.EventId).First().IsPrivate = Data.EventData.IsPrivate;
            _context.Events.Where(w => w.EventId.ToString() == Data.EventData.EventId).First().BasePauseTime = Data.EventData.BasePauseTime;
            _context.Events.Where(w => w.EventId.ToString() == Data.EventData.EventId).First().LocationPauseTime = Data.EventData.LocationPauseTime;
            _context.Events.Where(w => w.EventId.ToString() == Data.EventData.EventId).First().LocWeight = Data.EventData.LocWeight;
            _context.Events.Where(w => w.EventId.ToString() == Data.EventData.EventId).First().GroupWeight = Data.EventData.GroupWeight;
            _context.Events.Where(w => w.EventId.ToString() == Data.EventData.EventId).First().TypeWeight = Data.EventData.TypeWeight;
            _context.Events.Where(w => w.EventId.ToString() == Data.EventData.EventId).First().CompWeight = Data.EventData.CompWeight;

            var EventTypesToRemove = _context.EventTypes.Where(w => w.EventId == id).ToList();
            _context.EventTypes.RemoveRange(EventTypesToRemove);
            var GroupsToRemove = _context.Groups.Where(w => w.EventId == id).ToList();
            _context.Groups.RemoveRange(GroupsToRemove);
            var LocationsToRemove = _context.Locations.Where(w => w.EventId == id).ToList();
            _context.Locations.RemoveRange(LocationsToRemove);
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


            var EventTypeMap = new Dictionary<string, EventType>();
            foreach (EventTypeDTO item in Data.EventTypes)
            {
                EventType temp = new EventType();
                temp.EventId = id;
                temp.TypeName = item.TypeName;
                temp.TimeRange = item.TimeRange;
                _context.EventTypes.Add(temp);
                EventTypeMap[item.EventTypeId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var GroupMap = new Dictionary<string, Group>();
            foreach (GroupDTO item in Data.Groups)
            {
                Group temp = new Group();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.GroupName = item.GroupName;
                _context.Groups.Add(temp);
                GroupMap[item.GroupId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var LocationMap = new Dictionary<string, Location>();
            foreach (LocationDTO item in Data.Locations)
            {
                Location temp = new Location();
                temp.LocationName = item.LocationName;
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.Slots = item.Slots;
                _context.Locations.Add(temp);
                LocationMap[item.LocationId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var LocationTableMap = new Dictionary<string, LocationTable>();
            foreach (LocationTableDTO item in Data.LocationTable)
            {
                LocationTable temp = new LocationTable();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.LocationId = LocationMap[item.LocationId].LocationId;
                temp.EventTypeId = EventTypeMap[item.EventTypeId].EventTypeId;
                _context.LocationTables.Add(temp);
                LocationTableMap[item.LocationTableId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var ParticipantMap = new Dictionary<string, Participant>();
            foreach (ParticipantDTO item in Data.Participants)
            {
                Participant temp = new Participant();
                temp.ParticipantName = item.ParticipantName;
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.CompetitorNumber = item.CompetitorNumber;
                if (item.GroupId != null && item.GroupId != "") temp.GroupId = GroupMap[item.GroupId].GroupId;
                _context.Participants.Add(temp);
                ParticipantMap[item.ParticipantId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var pauseTableMap = new Dictionary<string, PauseTable>();
            foreach (PauseTableDTO item in Data.PauseTable)
            {
                PauseTable temp = new PauseTable();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.LocationId1 = LocationMap[item.LocationId1].LocationId;
                temp.LocationId2 = LocationMap[item.LocationId2].LocationId;
                temp.Pause = item.Pause;
                _context.PauseTables.Add(temp);
                pauseTableMap[item.PauseId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

            var registrationMap = new Dictionary<string, Registration>();
            foreach (RegistrationDTO item in Data.Registrations)
            {
                Registration temp = new Registration();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.ParticipantId = ParticipantMap[item.ParticipantId].ParticipantId;
                temp.EventTypeId = EventTypeMap[item.EventTypeId].EventTypeId;
                _context.Registrations.Add(temp);
                registrationMap[item.RegistrationId] = temp;
            }
            try { _context.SaveChanges(); } catch { return false; }

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

            request.EventId = id;

            List<LocationModel> LML = new List<LocationModel>();
            foreach (Location item in _context.Locations.Where(w => w.EventId == id))
            {
                LocationModel LM = new LocationModel();
                LM.Id = item.LocationId;
                LM.Name = item.LocationName;
                LM.Capacity = Convert.ToInt32(item.Slots);
                LML.Add(LM);
            }
            request.Locations = LML;

            var EML = _context.EventTypes.Where(e => e.EventId == id).Select(e => new EventModel
            {
                Id = e.EventTypeId,
                Name = e.TypeName,
                Duration = e.TimeRange.Hour * 60 + e.TimeRange.Minute,
                PossibleLocations = _context.LocationTables
                    .Where(l => l.EventTypeId == e.EventTypeId)
                    .Select(l => l.LocationId)
                    .ToList()
            }).ToList();
            request.Events = EML;

            List<CompetitorModel> CML = new List<CompetitorModel>();
            foreach (Participant item in _context.Participants.Where(w => w.EventId == id))
            {
                CompetitorModel CM = new CompetitorModel();
                CM.Id = item.ParticipantId;
                CM.Name = item.ParticipantName;
                CM.GroupId = item.GroupId ?? -1;
                CML.Add(CM);
            }
            request.Competitors = CML;

            List<EntryModel> EnML = new List<EntryModel>();
            foreach (Registration item in _context.Registrations.Where(w => w.EventId == id))
            {
                EntryModel EnM = new EntryModel();
                EnM.Id = item.RegistrationId;
                EnM.EventId = item.EventTypeId;
                EnM.CompetitorId = item.ParticipantId;
                EnML.Add(EnM);
            }
            request.Entries = EnML;

            var EventStart = _context.Events.Where(w => w.EventId == id).Select(s => s.StartDate).First();
            List<ConstraintModel> CoML = new List<ConstraintModel>();
            foreach (Constraint item in _context.Constraints.Where(w => w.EventId == id))
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
            foreach (PauseTable item in _context.PauseTables.Where(w => w.EventId == id))
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
            request.MaxDays = (_context.Events.Where(w => w.EventId == id).First().EndDate - _context.Events.Where(w => w.EventId == id).FirstOrDefault().StartDate).Days + 1;
            request.BreakTimeLoc = _context.Events.Where(w => w.EventId == id).First().LocationPauseTime;
            request.BasePauseTime = _context.Events.Where(w => w.EventId == id).First().BasePauseTime;
            request.LocWeight = Math.Min(500,_context.Events.Where(w => w.EventId == id).First().LocWeight);
            request.GroupWeight = Math.Min(500, _context.Events.Where(w => w.EventId == id).First().GroupWeight);
            request.TypeWeight = Math.Min(500, _context.Events.Where(w => w.EventId == id).First().TypeWeight);
            request.CompWeight = Math.Min(500, _context.Events.Where(w => w.EventId == id).First().CompWeight);
            return request;
        }
        public bool NewSchedule(List<ScheduleModel> request, int Event_id)
        {
            List<long> typeIDs = _context.EventTypes.Where(w => w.EventId == Event_id).Select(s => s.EventTypeId).ToList();
            DateTime EventStart = _context.Events.Where(w => w.EventId == Event_id).Select(s => s.StartDate).First();
            var itemsToRemove = _context.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)).ToList();
            _context.Schedules.RemoveRange(itemsToRemove);

            foreach (var item in request)
            {
                Models.Schedule NS = new Models.Schedule();
                NS.ParticipantId = item.ParticipantId;
                NS.StartTime = EventStart.AddMinutes(item.Start);
                NS.EndTime = EventStart.AddMinutes(item.End);
                NS.LocationId = item.LocationId;
                NS.Slot = item.Slot;
                NS.EventTypeId = item.EventTypeId;
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

            var Schedules = _context.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)).ToList();
            var pause = _context.Events.Where(w => w.EventId == Event_id).First().LocationPauseTime;
            var Locations = _context.Locations.ToList();

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
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }
        public DataDTO GetScheduleData(string id)
        {
            DataDTO TempData = new DataDTO();
            TempData.EventData.EventId = id;
            TempData.EventData.EventName = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EventName;
            TempData.EventData.StartDate = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().StartDate;
            TempData.EventData.EndDate = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EndDate;
            List<long> typeIDs = _context.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)).Select(s => s.EventTypeId).ToList();

            DateTime temp_Startdate = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().StartDate;
            List <TimeZoneDTO> TZL = new List<TimeZoneDTO>();
            foreach (Models.Schedule item in _context.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)))
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
            foreach (EventType item in _context.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)))
            {
                EventTypeDTO ET = new EventTypeDTO();
                ET.EventTypeId = item.EventTypeId.ToString();
                ET.TypeName = item.TypeName;
                ETL.Add(ET);
            }
            TempData.EventTypes = ETL;

            List<ParticipantDTO> SPL = new List<ParticipantDTO>();
            foreach (Participant item in _context.Participants.Where(e => e.EventId == Convert.ToInt32(id)))
            {
                ParticipantDTO SP = new ParticipantDTO();
                SP.ParticipantId = item.ParticipantId.ToString();
                SP.ParticipantName = item.ParticipantName;
                SPL.Add(SP);
            }
            TempData.Participants = SPL;

            List<LocationDTO> SLL = new List<LocationDTO>();
            foreach (Location item in _context.Locations.Where(e => e.EventId == Convert.ToInt32(id)))
            {
                LocationDTO SL = new LocationDTO();
                SL.LocationId = item.LocationId.ToString();
                SL.LocationName = item.LocationName;
                SLL.Add(SL);
            }
            TempData.Locations = SLL;

            List<ConstraintDTO> SCL = new List<ConstraintDTO>();
            foreach (Constraint item in _context.Constraints.Where(e => e.EventId == Convert.ToInt32(id)))
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
        public ScheduleDataForEXPORT GetScheduleDataEXPORT(string id)
        {
            ScheduleDataForEXPORT TempData = new ScheduleDataForEXPORT();
            TempData.EventName = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EventName;
            TempData.StartDate = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().StartDate;
            TempData.EndDate = _context.Events.Where(e => e.EventId == Convert.ToInt32(id)).First().EndDate;
            List<long> typeIDs = _context.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)).Select(s => s.EventTypeId).ToList();

            var EventTypes = _context.EventTypes.Where(e => e.EventId == Convert.ToInt32(id)).ToList();
            var Participants = _context.Participants.Where(e => e.EventId == Convert.ToInt32(id)).ToList();
            var Locations = _context.Locations.Where(e => e.EventId == Convert.ToInt32(id)).ToList();
            var Groups = _context.Groups.Where(e => e.EventId == Convert.ToInt32(id)).ToList();

            List<ScheduleTimeZoneEXPORT> TZL = new List<ScheduleTimeZoneEXPORT>();
            foreach (Models.Schedule item in _context.Schedules.Where(w => typeIDs.Contains(w.EventTypeId)))
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
