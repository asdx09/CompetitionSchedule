using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2010.Excel;
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
        const int MinuteConversion = 1;

        private readonly ScheduleLogicContext _dbService;

        public DatabaseService(ScheduleLogicContext context)
        {
            _dbService = context;

        }
        public async Task<User?> LoginUser(string username, string password)
        {
            Console.WriteLine(username);
            var user = await _dbService.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) user = await _dbService.Users.FirstOrDefaultAsync(u => u.Email == username);
            if (user == null || user.Validated != "") return null;

            var passwordHasher = new PasswordHasher<User>();
            var result = passwordHasher.VerifyHashedPassword(user, user.Password, password);

            return result == PasswordVerificationResult.Success ? user : null;
        }
        public async Task<string> RegisterUser(string username, string password, string email, AuthenticationService Auth_Service)
        {
            if (await _dbService.Users.AnyAsync(u => u.Username == username))
                return "Username already taken!";

            if (await _dbService.Users.AnyAsync(u => u.Email == email))
                return "Email already taken!";

            var passwordHasher = new PasswordHasher<User>();
            var user = new User
            {
                Username = username
            };
            user.Password = passwordHasher.HashPassword(user, password);
            user.Email = email;
            var token = Guid.NewGuid().ToString();
            user.Validated = token;
            await Auth_Service.NewValidationEmail(token, email, username);

            await _dbService.Users.AddAsync(user);
            await _dbService.SaveChangesAsync();
            return "";
        }
        public async Task<long> NewEvent(string username)
        {
            Event newEvent = new Event
            {
                Createdby = await GetUserID(username),
                Eventname = "New Event",
                Startdate = DateTime.UtcNow,
                Enddate = DateTime.UtcNow,
                Basepausetime = 5,
                Locationpausetime = 5,
                Isprivate = true,
                Locweight = 1,
                Compweight = 1,
                Groupweight = 1,
                Typeweight = 1
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
                existingEvent.Eventname = Data.EventData.EventName;
                existingEvent.Startdate = Data.EventData.StartDate.ToUniversalTime();
                existingEvent.Enddate = Data.EventData.EndDate.ToUniversalTime();
                existingEvent.Isprivate = Data.EventData.IsPrivate;
                existingEvent.Basepausetime = Data.EventData.BasePauseTime;
                existingEvent.Locationpausetime = Data.EventData.LocationPauseTime;
            }


            var EventTypeMap = new Dictionary<string, Eventtype>();
            foreach (EventTypeDTO item in Data.EventTypes)
            {
                Eventtype temp = new Eventtype();
                temp.EventId = id;
                temp.Typename = item.TypeName;
                temp.Timerange = item.TimeRange;
                _dbService.Eventtypes.Add(temp);
                EventTypeMap[item.EventTypeId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var GroupMap = new Dictionary<string, Group>();
            foreach (GroupDTO item in Data.Groups)
            {
                Group temp = new Group();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.Groupname = item.GroupName;
                _dbService.Groups.Add(temp);
                GroupMap[item.GroupId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var LocationMap = new Dictionary<string, Location>();
            foreach (LocationDTO item in Data.Locations)
            {
                Location temp = new Location();
                temp.Locationname = item.LocationName;
                temp.EventId = (long)Convert.ToDouble(id);
                temp.Slots = item.Slots;
                _dbService.Locations.Add(temp);
                LocationMap[item.LocationId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var LocationTableMap = new Dictionary<string, Locationtable>();
            foreach (LocationTableDTO item in Data.LocationTable)
            {
                Locationtable temp = new Locationtable();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.LocationId = LocationMap[item.LocationId].LocationId;
                temp.EventtypeId = EventTypeMap[item.EventTypeId].EventtypeId;
                _dbService.Locationtables.Add(temp);
                LocationTableMap[item.LocationTableId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var ParticipantMap = new Dictionary<string, Participant>();
            foreach (ParticipantDTO item in Data.Participants)
            {
                Participant temp = new Participant();
                temp.Participantname = item.ParticipantName;
                temp.EventId = (long)Convert.ToDouble(id);
                temp.Competitornumber = item.CompetitorNumber;
                if (item.GroupId != null && item.GroupId != "") temp.GroupId = GroupMap[item.GroupId].GroupId;
                _dbService.Participants.Add(temp);
                ParticipantMap[item.ParticipantId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var pauseTableMap = new Dictionary<string, Pausetable>();
            foreach (PauseTableDTO item in Data.PauseTable)
            {
                Pausetable temp = new Pausetable();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.LocationId1 = LocationMap[item.LocationId1].LocationId;
                temp.LocationId2 = LocationMap[item.LocationId2].LocationId;
                temp.Pause = item.Pause;
                _dbService.Pausetables.Add(temp);
                pauseTableMap[item.PauseId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var registrationMap = new Dictionary<string, Registration>();
            foreach (RegistrationDTO item in Data.Registrations)
            {
                Registration temp = new Registration();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.ParticipantId = ParticipantMap[item.ParticipantId].ParticipantId;
                temp.EventtypeId = EventTypeMap[item.EventTypeId].EventtypeId;
                _dbService.Registrations.Add(temp);
                registrationMap[item.RegistrationId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var ConstraintMap = new Dictionary<string, Eventconstraint>();
            foreach (ConstraintDTO item in Data.Constraints)
            {
                Eventconstraint temp = new Eventconstraint();
                temp.EventId = (long)Convert.ToDouble(id);
                temp.Constrainttype = item.ConstraintType[0];
                try
                {
                    switch (temp.Constrainttype)
                    {
                        case 'L':
                            temp.ObjectId = LocationMap[item.ObjectId].LocationId;
                            break;
                        case 'G':
                            temp.ObjectId = GroupMap[item.ObjectId].GroupId;
                            break;
                        case 'C':
                            temp.ObjectId = ParticipantMap[item.ObjectId].ParticipantId;
                            break;
                        case 'T':
                            temp.ObjectId = EventTypeMap[item.ObjectId].EventtypeId;
                            break;
                    }
                }
                catch { return false; }
                temp.Starttime = item.StartTime;
                temp.Endtime = item.EndTime;
                _dbService.Eventconstraints.Add(temp);
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
        public async Task DeleteEvent(long id)
        {
            var EventToRemove = await _dbService.Events.Where(w => w.EventId == id).ToListAsync();
            _dbService.Events.RemoveRange(EventToRemove);
            var pauesToRemove = await _dbService.Pausetables.Where(w => w.EventId == id).ToListAsync();
            _dbService.Pausetables.RemoveRange(pauesToRemove);
            var LocationTableToRemove = await _dbService.Locationtables.Where(w => w.EventId == id).ToListAsync();
            _dbService.Locationtables.RemoveRange(LocationTableToRemove);
            var LocationsToRemove = await _dbService.Locations.Where(w => w.EventId == id).ToListAsync();
            _dbService.Locations.RemoveRange(LocationsToRemove);
            var SchedulesToRemove = await _dbService.Schedules.Where(w => w.EventtypeId == id).ToListAsync();
            _dbService.Schedules.RemoveRange(SchedulesToRemove);
            var registrationsToRemove = await _dbService.Registrations.Where(w => w.EventId == id).ToListAsync();
            _dbService.Registrations.RemoveRange(registrationsToRemove);
            var ParticipantsToRemove = await _dbService.Participants.Where(w => w.EventId == id).ToListAsync();
            _dbService.Participants.RemoveRange(ParticipantsToRemove);
            var GroupsToRemove = await _dbService.Groups.Where(w => w.EventId == id).ToListAsync();
            _dbService.Groups.RemoveRange(GroupsToRemove);
            var EventtypesToRemove = await _dbService.Eventtypes.Where(w => w.EventId == id).ToListAsync();
            _dbService.Eventtypes.RemoveRange(EventtypesToRemove);
            var constraintsToRemove = await _dbService.Eventconstraints.Where(w => w.EventId == id).ToListAsync();
            _dbService.Eventconstraints.RemoveRange(constraintsToRemove);
            await _dbService.SaveChangesAsync();
        }
        public async Task<long> GetUserID(string email)
        {
            if (await _dbService.Users.CountAsync() < 1) return 0;
            return _dbService.Users.Where(w => w.Email == email).First().UserId;
        }
        public async Task<string> GetUsername(string email)
        {
            if (await _dbService.Users.CountAsync() < 1) return "";
            return _dbService.Users.Where(w => w.Email == email).First().Username;
        }
        public async Task<bool> CheckUser(string username, string id)
        {
            var userId = await GetUserID(username);
            return await _dbService.Events.Where(w => w.Createdby == userId && w.EventId == Convert.ToInt32(id)).AnyAsync();
        }
        public async Task<bool> CheckUserExist(string email)
        {
            return await _dbService.Users.Where(w => w.Email == email).CountAsync() > 0;
        }
        public async Task<List<EventsData>> GetEvents(string username)
        {
            var userId = await GetUserID(username);
            return await _dbService.Events.Where(e => e.Createdby == userId).Select(e => new EventsData { EventId = e.EventId, EventName = e.Eventname }).ToListAsync();
        }
        public async Task<DataDTO> GetEvent(string id, string username)
        {
            DataDTO TempData = new DataDTO();
            var userId = await GetUserID(username);
            TempData.EventData = await _dbService.Events.AsNoTracking().Where(w => w.Createdby == userId && w.EventId == Convert.ToInt32(id)).Select(e => new EventDTO
            {
                EventId = e.EventId.ToString(),
                EventName = e.Eventname,
                StartDate = e.Startdate,
                EndDate = e.Enddate,
                IsPrivate = e.Isprivate,
                BasePauseTime = e.Basepausetime,
                LocationPauseTime = e.Locationpausetime,
                LocWeight = e.Locweight,
                GroupWeight = e.Groupweight,
                TypeWeight = e.Typeweight,
                CompWeight = e.Compweight
            }).FirstAsync();
            TempData.EventTypes = await _dbService.Eventtypes.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new EventTypeDTO
            {
                EventId = e.EventId.ToString(),
                EventTypeId = e.EventtypeId.ToString(),
                TypeName = e.Typename,
                TimeRange = e.Timerange
            }).ToListAsync();
            TempData.Groups = await _dbService.Groups.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new GroupDTO
            {
                GroupId = e.GroupId.ToString(),
                EventId = e.EventId.ToString(),
                GroupName = e.Groupname
            }).ToListAsync();
            TempData.Locations = await _dbService.Locations.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new LocationDTO
            {
                LocationId = e.LocationId.ToString(),
                EventId = e.EventId.ToString(),
                LocationName = e.Locationname,
                Slots = e.Slots
            }).ToListAsync();
            TempData.Participants = await _dbService.Participants.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new ParticipantDTO
            {
                ParticipantId = e.ParticipantId.ToString(),
                ParticipantName = e.Participantname,
                CompetitorNumber = e.Competitornumber,
                EventId = e.EventId.ToString(),
                GroupId = e.GroupId.ToString()
            }).ToListAsync();
            TempData.Registrations = await _dbService.Registrations.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new RegistrationDTO
            {
                RegistrationId = e.RegistrationId.ToString(),
                EventId = e.EventId.ToString(),
                ParticipantId = e.ParticipantId.ToString(),
                EventTypeId = e.EventtypeId.ToString()
            }).ToListAsync();
            TempData.PauseTable = await _dbService.Pausetables.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new PauseTableDTO
            {
                PauseId = e.PauseId.ToString(),
                EventId = e.EventId.ToString(),
                LocationId1 = e.LocationId1.ToString(),
                LocationId2 = e.LocationId2.ToString(),
                Pause = e.Pause
            }).ToListAsync();
            TempData.LocationTable = await _dbService.Locationtables.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new LocationTableDTO
            {
                LocationTableId = e.LocationtableId.ToString(),
                EventId = e.EventId.ToString(),
                EventTypeId = e.EventtypeId.ToString(),
                LocationId = e.LocationId.ToString()
            }).ToListAsync();
            TempData.Constraints = await _dbService.Eventconstraints.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new ConstraintDTO
            {
                ConstraintId = e.ConstraintId.ToString(),
                EventId = e.EventId.ToString(),
                ObjectId = e.ObjectId.ToString(),
                ConstraintType = e.Constrainttype.ToString(),
                StartTime = e.Starttime,
                EndTime = e.Endtime
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
                existingEvent.Eventname = Data.EventData.EventName;
                existingEvent.Startdate = Data.EventData.StartDate.ToUniversalTime();
                existingEvent.Enddate = Data.EventData.EndDate.ToUniversalTime();
                existingEvent.Isprivate = Data.EventData.IsPrivate;
                existingEvent.Basepausetime = Data.EventData.BasePauseTime;
                existingEvent.Locationpausetime = Data.EventData.LocationPauseTime;
                existingEvent.Locweight = Data.EventData.LocWeight;
                existingEvent.Groupweight = Data.EventData.GroupWeight;
                existingEvent.Typeweight = Data.EventData.TypeWeight;
                existingEvent.Compweight = Data.EventData.CompWeight;
            }

            var EventtypesToRemove = _dbService.Eventtypes.Where(w => w.EventId == id).ToList();
            _dbService.Eventtypes.RemoveRange(EventtypesToRemove);
            var GroupsToRemove = _dbService.Groups.Where(w => w.EventId == id).ToList();
            _dbService.Groups.RemoveRange(GroupsToRemove);
            var LocationsToRemove = _dbService.Locations.Where(w => w.EventId == id).ToList();
            _dbService.Locations.RemoveRange(LocationsToRemove);
            var LocationTableToRemove = _dbService.Locationtables.Where(w => w.EventId == id).ToList();
            _dbService.Locationtables.RemoveRange(LocationTableToRemove);
            var ParticipantsToRemove = _dbService.Participants.Where(w => w.EventId == id).ToList();
            _dbService.Participants.RemoveRange(ParticipantsToRemove);
            var PauseTableToRemove = _dbService.Pausetables.Where(w => w.EventId == id).ToList();
            _dbService.Pausetables.RemoveRange(PauseTableToRemove);
            var RegistrationToRemove = _dbService.Registrations.Where(w => w.EventId == id).ToList();
            _dbService.Registrations.RemoveRange(RegistrationToRemove);
            var ConstraintsToRemove = _dbService.Eventconstraints.Where(w => w.EventId == id).ToList();
            _dbService.Eventconstraints.RemoveRange(ConstraintsToRemove);

            await _dbService.SaveChangesAsync();

            var EventTypeMap = new Dictionary<string, Eventtype>();
            foreach (EventTypeDTO item in Data.EventTypes)
            {
                Eventtype temp = new Eventtype();
                temp.EventId = id;
                temp.Typename = item.TypeName;
                temp.Timerange = item.TimeRange;
                _dbService.Eventtypes.Add(temp);
                EventTypeMap[item.EventTypeId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var GroupMap = new Dictionary<string, Group>();
            foreach (GroupDTO item in Data.Groups)
            {
                Group temp = new Group();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.Groupname = item.GroupName;
                _dbService.Groups.Add(temp);
                GroupMap[item.GroupId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var LocationMap = new Dictionary<string, Location>();
            foreach (LocationDTO item in Data.Locations)
            {
                Location temp = new Location();
                temp.Locationname = item.LocationName;
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.Slots = item.Slots;
                _dbService.Locations.Add(temp);
                LocationMap[item.LocationId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var LocationTableMap = new Dictionary<string, Locationtable>();
            foreach (LocationTableDTO item in Data.LocationTable)
            {
                Locationtable temp = new Locationtable();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.LocationId = LocationMap[item.LocationId].LocationId;
                temp.EventtypeId = EventTypeMap[item.EventTypeId].EventtypeId;
                _dbService.Locationtables.Add(temp);
                LocationTableMap[item.LocationTableId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var ParticipantMap = new Dictionary<string, Participant>();
            foreach (ParticipantDTO item in Data.Participants)
            {
                Participant temp = new Participant();
                temp.Participantname = item.ParticipantName;
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.Competitornumber = item.CompetitorNumber;
                if (item.GroupId != null && item.GroupId != "") temp.GroupId = GroupMap[item.GroupId].GroupId;
                _dbService.Participants.Add(temp);
                ParticipantMap[item.ParticipantId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var pauseTableMap = new Dictionary<string, Pausetable>();
            foreach (PauseTableDTO item in Data.PauseTable)
            {
                Pausetable temp = new Pausetable();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.LocationId1 = LocationMap[item.LocationId1].LocationId;
                temp.LocationId2 = LocationMap[item.LocationId2].LocationId;
                temp.Pause = item.Pause;
                _dbService.Pausetables.Add(temp);
                pauseTableMap[item.PauseId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var registrationMap = new Dictionary<string, Registration>();
            foreach (RegistrationDTO item in Data.Registrations)
            {
                Registration temp = new Registration();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.ParticipantId = ParticipantMap[item.ParticipantId].ParticipantId;
                temp.EventtypeId = EventTypeMap[item.EventTypeId].EventtypeId;
                _dbService.Registrations.Add(temp);
                registrationMap[item.RegistrationId] = temp;
            }

            await _dbService.SaveChangesAsync();

            var ConstraintMap = new Dictionary<string, Eventconstraint>();
            foreach (ConstraintDTO item in Data.Constraints)
            {
                Eventconstraint temp = new Eventconstraint();
                temp.EventId = (long)Convert.ToDouble(Data.EventData.EventId);
                temp.Constrainttype = item.ConstraintType[0];
                try
                {
                    switch (temp.Constrainttype)
                    {
                        case 'L':
                            temp.ObjectId = LocationMap[item.ObjectId].LocationId;
                            break;
                        case 'G':
                            temp.ObjectId = GroupMap[item.ObjectId].GroupId;
                            break;
                        case 'C':
                            temp.ObjectId = ParticipantMap[item.ObjectId].ParticipantId;
                            break;
                        case 'T':
                            temp.ObjectId = EventTypeMap[item.ObjectId].EventtypeId;
                            break;
                    }
                }
                catch { return false; }
                temp.Starttime = item.StartTime.ToUniversalTime();
                temp.Endtime = item.EndTime.ToUniversalTime();
                _dbService.Eventconstraints.Add(temp);
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
                    Name = l.Locationname,
                    Capacity = (int)l.Slots
                })
                .ToListAsync();

            // Eventtypes + PossibleLocations
            var Eventtypes = await _dbService.Eventtypes
                .Where(e => e.EventId == id)
                .ToListAsync();

            var Locationtables = await _dbService.Locationtables
                .Where(lt => Eventtypes.Select(e => e.EventtypeId).Contains(lt.EventtypeId))
                .ToListAsync();

            request.Events = Eventtypes.Select(e => new EventModel
            {
                Id = e.EventtypeId,
                Name = e.Typename,
                Duration = (e.Timerange.Hour * 60 + e.Timerange.Minute) / MinuteConversion,
                PossibleLocations = Locationtables
                    .Where(lt => lt.EventtypeId == e.EventtypeId)
                    .Select(lt => lt.LocationId)
                    .ToList()
            }).ToList();

            // Participants
            request.Competitors = await _dbService.Participants
                .Where(p => p.EventId == id)
                .Select(p => new CompetitorModel
                {
                    Id = p.ParticipantId,
                    Name = p.Participantname,
                    GroupId = p.GroupId ?? -1
                })
                .ToListAsync();

            // Registrations
            request.Entries = await _dbService.Registrations
                .Where(r => r.EventId == id)
                .Select(r => new EntryModel
                {
                    Id = r.RegistrationId,
                    EventId = r.EventtypeId,
                    CompetitorId = r.ParticipantId
                })
                .ToListAsync();

            // Constraints
            var constraints = await _dbService.Eventconstraints
                .Where(c => c.EventId == id)
                .ToListAsync();

            request.Constraints = constraints.Select(c => new ConstraintModel
            {
                Id = c.ConstraintId,
                ConstraintType = c.Constrainttype.ToString(),
                ObjectId = c.ObjectId,
                StartTime = (int)(c.Starttime - evt.Startdate).TotalMinutes / MinuteConversion,
                EndTime = (int)(c.Endtime - evt.Startdate).TotalMinutes / MinuteConversion
            }).ToList();

            // PauseTable / Travel
            request.Travel = await _dbService.Pausetables
                .Where(p => p.EventId == id)
                .Select(p => new PauseTableModel
                {
                    Id = p.PauseId,
                    LocationID1 = p.LocationId1,
                    LocationID2 = p.LocationId2,
                    Pause = p.Pause.Hour * 60 + p.Pause.Minute / MinuteConversion
                })
                .ToListAsync();

            // Event info
            request.DayLength = (24 * 60 - 1) / MinuteConversion;
            request.MaxDays = (evt.Enddate - evt.Startdate).Days + 1;
            request.BreakTimeLoc = evt.Locationpausetime / MinuteConversion;
            request.BasePauseTime = evt.Basepausetime / MinuteConversion;
            request.LocWeight = Math.Min(500, evt.Locweight);
            request.GroupWeight = Math.Min(500, evt.Groupweight);
            request.TypeWeight = Math.Min(500, evt.Typeweight);
            request.CompWeight = Math.Min(500, evt.Compweight);

            return request;
        }
        public async Task<bool> NewSchedule(List<ScheduleModel> request, int Event_id)
        {
            List<long> typeIDs = await _dbService.Eventtypes.Where(w => w.EventId == Event_id).Select(s => s.EventtypeId).ToListAsync();
            DateTime EventStart = _dbService.Events.Where(w => w.EventId == Event_id).Select(s => s.Startdate).First();
            var itemsToRemove = await _dbService.Schedules.Where(w => typeIDs.Contains(w.EventtypeId)).ToListAsync();
            _dbService.Schedules.RemoveRange(itemsToRemove);

            foreach (var item in request)
            {
                Models.Schedule NS = new Models.Schedule();
                NS.ParticipantId = item.ParticipantId;
                NS.Starttime = EventStart.AddMinutes(item.Start * MinuteConversion);
                NS.Endtime = EventStart.AddMinutes(item.End * MinuteConversion);
                NS.LocationId = item.LocationId;
                NS.Slot = item.Slot;
                NS.EventtypeId = item.EventTypeId;
                _dbService.Schedules.Add(NS);
            }

            await _dbService.SaveChangesAsync();

            var Schedules = await _dbService.Schedules.Where(w => typeIDs.Contains(w.EventtypeId)).ToListAsync();
            var pause = _dbService.Events.Where(w => w.EventId == Event_id).First().Locationpausetime;
            var Locations = await _dbService.Locations.ToListAsync();

            var GroupedByLocation = Schedules.GroupBy(s => s.LocationId);

            foreach (var Group in GroupedByLocation)
            {
                var entries = Group.OrderBy(s => s.Starttime).ToList();
                var activeSlots = new List<Models.Schedule>();

                int max = Locations
                    .First(l => l.LocationId == Group.Key)
                    .Slots;

                foreach (var e in entries)
                {
                    activeSlots = activeSlots
                        .Where(a => a.Endtime + new TimeSpan(0, 5, 0) > e.Starttime)
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
        public async Task<DataDTO> GetScheduleData(string id, string username)
        {
            DataDTO TempData = new DataDTO();
            var userId = await GetUserID(username);
            TempData.EventData = await _dbService.Events.AsNoTracking().Where(w => w.Createdby == userId && w.EventId == Convert.ToInt32(id)).Select(e => new EventDTO
            {
                EventId = e.EventId.ToString(),
                EventName = e.Eventname,
                StartDate = e.Startdate,
                EndDate = e.Enddate,
                IsPrivate = e.Isprivate,
                BasePauseTime = e.Basepausetime,
                LocationPauseTime = e.Locationpausetime,
                LocWeight = e.Locweight,
                GroupWeight = e.Groupweight,
                TypeWeight = e.Typeweight,
                CompWeight = e.Compweight
            }).FirstAsync();
            TempData.EventTypes = await _dbService.Eventtypes.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new EventTypeDTO
            {
                EventId = e.EventId.ToString(),
                EventTypeId = e.EventtypeId.ToString(),
                TypeName = e.Typename,
                TimeRange = e.Timerange
            }).ToListAsync();
            TempData.Groups = await _dbService.Groups.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new GroupDTO
            {
                GroupId = e.GroupId.ToString(),
                EventId = e.EventId.ToString(),
                GroupName = e.Groupname
            }).ToListAsync();
            TempData.Locations = await _dbService.Locations.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new LocationDTO
            {
                LocationId = e.LocationId.ToString(),
                EventId = e.EventId.ToString(),
                LocationName = e.Locationname,
                Slots = e.Slots
            }).ToListAsync();
            TempData.Participants = await _dbService.Participants.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new ParticipantDTO
            {
                ParticipantId = e.ParticipantId.ToString(),
                ParticipantName = e.Participantname,
                CompetitorNumber = e.Competitornumber,
                EventId = e.EventId.ToString(),
                GroupId = e.GroupId.ToString()
            }).ToListAsync();
            TempData.Registrations = await _dbService.Registrations.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new RegistrationDTO
            {
                RegistrationId = e.RegistrationId.ToString(),
                EventId = e.EventId.ToString(),
                ParticipantId = e.ParticipantId.ToString(),
                EventTypeId = e.EventtypeId.ToString()
            }).ToListAsync();
            TempData.PauseTable = await _dbService.Pausetables.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new PauseTableDTO
            {
                PauseId = e.PauseId.ToString(),
                EventId = e.EventId.ToString(),
                LocationId1 = e.LocationId1.ToString(),
                LocationId2 = e.LocationId2.ToString(),
                Pause = e.Pause
            }).ToListAsync();
            TempData.LocationTable = await _dbService.Locationtables.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new LocationTableDTO
            {
                LocationTableId = e.LocationtableId.ToString(),
                EventId = e.EventId.ToString(),
                EventTypeId = e.EventtypeId.ToString(),
                LocationId = e.LocationId.ToString()
            }).ToListAsync();
            TempData.Constraints = await _dbService.Eventconstraints.AsNoTracking().Where(w => w.EventId == Convert.ToInt32(id)).Select(e => new ConstraintDTO
            {
                ConstraintId = e.ConstraintId.ToString(),
                EventId = e.EventId.ToString(),
                ObjectId = e.ObjectId.ToString(),
                ConstraintType = e.Constrainttype.ToString(),
                StartTime = e.Starttime,
                EndTime = e.Endtime
            }).ToListAsync();

            var typeIDs = await _dbService.Eventtypes
                .Where(e => e.EventId == Convert.ToInt32(id))
                .Select(e => e.EventtypeId)
                .ToListAsync();

            var startDate = _dbService.Events.AsNoTracking().Where(w => w.Createdby == userId && w.EventId == Convert.ToInt32(id)).First().Startdate.ToLocalTime();

            TempData.TimeZones = await _dbService.Schedules.AsNoTracking().Where(w => typeIDs.Contains(w.EventtypeId)).Select(e => new TimeZoneDTO
            {
                ScheduleId = e.ScheduleId,
                EventTypeId = e.EventtypeId,
                ParticipantId = e.ParticipantId,
                LocationId = e.LocationId,
                StartTime = Convert.ToInt32((e.Starttime.ToLocalTime() - startDate.Date).TotalMinutes),
                EndTime = Convert.ToInt32((e.Endtime.ToLocalTime() - startDate.Date).TotalMinutes),
                Slot = e.Slot
            }).ToListAsync();

            return TempData;
        }
        public async Task<ScheduleDataForEXPORT> GetScheduleDataEXPORT(string id)
        {
            int eventId = Convert.ToInt32(id);

            var evt = await _dbService.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (evt == null) return null;

            var typeIDs = await _dbService.Eventtypes
                .Where(e => e.EventId == eventId)
                .Select(e => e.EventtypeId)
                .ToListAsync();

            var Eventtypes = await _dbService.Eventtypes
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
                .Where(s => typeIDs.Contains(s.EventtypeId))
                .ToListAsync();

            var eventTypeDict = Eventtypes.ToDictionary(e => e.EventtypeId, e => e.Typename);
            var participantDict = participants.ToDictionary(p => p.ParticipantId, p => p);
            var locationDict = locations.ToDictionary(l => l.LocationId, l => l.Locationname);
            var groupDict = groups.ToDictionary(g => g.GroupId, g => g.Groupname);

            var timeZones = schedules.Select(item =>
            {
                var participant = participantDict[item.ParticipantId];
                var groupName = participant.GroupId.HasValue && groupDict.ContainsKey(participant.GroupId.Value)
                    ? groupDict[participant.GroupId.Value]
                    : "";

                return new ScheduleTimeZoneEXPORT
                {
                    EventType = eventTypeDict[item.EventtypeId],
                    Participant = $"{participant.Participantname} ({participant.Competitornumber})",
                    Location = locationDict[item.LocationId],
                    StartTime = item.Starttime,
                    EndTime = item.Endtime,
                    Slot = item.Slot,
                    GroupName = groupName
                };
            }).ToList();

            return new ScheduleDataForEXPORT
            {
                EventName = evt.Eventname,
                StartDate = evt.Startdate,
                EndDate = evt.Enddate,
                TimeZones = timeZones
            };
        }
        public async Task<string> ValidateEmail(string token)
        {
            var user = await _dbService.Users.FirstOrDefaultAsync(u => u.Validated == token);
            if (user == null)
                return "Invalid or expired token.";

            user.Validated = "";
            await _dbService.SaveChangesAsync();
            return "";
        }
        public async Task DeleteUser(string username)
        {
            var userID = await GetUserID(username);

            List<Event> items = _dbService.Events.Where(w => w.Createdby == userID).ToList();
            foreach (Event item in items)
            {
                await DeleteEvent(item.EventId);
            }

            var ProfileToRemove = await _dbService.Users.Where(w => w.Email == username).ToListAsync();
            _dbService.Users.RemoveRange(ProfileToRemove);

            await _dbService.SaveChangesAsync();
        }
    }
}
