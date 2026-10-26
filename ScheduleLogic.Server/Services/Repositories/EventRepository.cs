using Microsoft.EntityFrameworkCore;
using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Models;
using ScheduleLogic.Server.Services.Interfaces;
using static ScheduleLogic.Server.Class.EventModels;

namespace ScheduleLogic.Server.Services
{
    public class EventRepository : IEventRepository
    {
        private readonly ScheduleLogicContext _db;

        public EventRepository(ScheduleLogicContext db)
        {
            _db = db;
        }

        public async Task<long> CreateEventAsync(long userId)
        {
            var newEvent = new Event
            {
                Createdby = userId,
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

            _db.Events.Add(newEvent);
            await _db.SaveChangesAsync();
            return newEvent.EventId;
        }

        public Task<bool> IsOwnedByUserAsync(long eventId, long userId) =>
            _db.Events.AnyAsync(e => e.EventId == eventId && e.Createdby == userId);

        // Feltételezi, hogy az event alatti táblák ON DELETE CASCADE-del
        // kapcsolódnak az event(event_id)-hez (lásd a javasolt sémát).
        // Ha ez nincs beállítva az adatbázisban, ez kivételt fog dobni
        // idegenkulcs-ütközés miatt.
        public async Task DeleteEventAsync(long eventId)
        {
            var evt = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
            if (evt == null) return;

            _db.Events.Remove(evt);
            await _db.SaveChangesAsync();
        }

        public Task<List<EventsData>> GetEventsAsync(long userId) =>
            _db.Events
                .Where(e => e.Createdby == userId)
                .Select(e => new EventsData { EventId = e.EventId, EventName = e.Eventname })
                .ToListAsync();

        public async Task<DataDTO?> GetEventAsync(long eventId)
        {
            var evt = await _db.Events.AsNoTracking()
                .Where(e => e.EventId == eventId)
                .Select(e => new EventDTO
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
                })
                .FirstOrDefaultAsync();

            if (evt == null) return null;

            var data = new DataDTO { EventData = evt };

            data.EventTypes = await _db.Eventtypes.AsNoTracking()
                .Where(w => w.EventId == eventId)
                .Select(e => new EventTypeDTO
                {
                    EventId = e.EventId.ToString(),
                    EventTypeId = e.EventtypeId.ToString(),
                    TypeName = e.Typename,
                    TimeRange = e.Timerange
                }).ToListAsync();

            data.Groups = await _db.Groups.AsNoTracking()
                .Where(w => w.EventId == eventId)
                .Select(e => new GroupDTO
                {
                    GroupId = e.GroupId.ToString(),
                    EventId = e.EventId.ToString(),
                    GroupName = e.Groupname
                }).ToListAsync();

            data.Locations = await _db.Locations.AsNoTracking()
                .Where(w => w.EventId == eventId)
                .Select(e => new LocationDTO
                {
                    LocationId = e.LocationId.ToString(),
                    EventId = e.EventId.ToString(),
                    LocationName = e.Locationname,
                    Slots = e.Slots
                }).ToListAsync();

            data.Participants = await _db.Participants.AsNoTracking()
                .Where(w => w.EventId == eventId)
                .Select(e => new ParticipantDTO
                {
                    ParticipantId = e.ParticipantId.ToString(),
                    ParticipantName = e.Participantname,
                    CompetitorNumber = e.Competitornumber,
                    EventId = e.EventId.ToString(),
                    GroupId = e.GroupId.ToString()
                }).ToListAsync();

            data.Registrations = await _db.Registrations.AsNoTracking()
                .Where(w => w.EventId == eventId)
                .Select(e => new RegistrationDTO
                {
                    RegistrationId = e.RegistrationId.ToString(),
                    EventId = e.EventId.ToString(),
                    ParticipantId = e.ParticipantId.ToString(),
                    EventTypeId = e.EventtypeId.ToString()
                }).ToListAsync();

            data.PauseTable = await _db.Pausetables.AsNoTracking()
                .Where(w => w.EventId == eventId)
                .Select(e => new PauseTableDTO
                {
                    PauseId = e.PauseId.ToString(),
                    EventId = e.EventId.ToString(),
                    LocationId1 = e.LocationId1.ToString(),
                    LocationId2 = e.LocationId2.ToString(),
                    Pause = e.Pause
                }).ToListAsync();

            data.LocationTable = await _db.Locationtables.AsNoTracking()
                .Where(w => w.EventId == eventId)
                .Select(e => new LocationTableDTO
                {
                    LocationTableId = e.LocationtableId.ToString(),
                    EventId = e.EventId.ToString(),
                    EventTypeId = e.EventtypeId.ToString(),
                    LocationId = e.LocationId.ToString()
                }).ToListAsync();

            data.Constraints = await _db.Eventconstraints.AsNoTracking()
                .Where(w => w.EventId == eventId)
                .Select(e => new ConstraintDTO
                {
                    ConstraintId = e.ConstraintId.ToString(),
                    EventId = e.EventId.ToString(),
                    ObjectId = e.ObjectId.ToString(),
                    ConstraintType = e.Constrainttype.ToString(),
                    StartTime = e.Starttime,
                    EndTime = e.Endtime
                }).ToListAsync();

            return data;
        }

        public async Task<bool> SaveEventAsync(DataDTO data)
        {
            long eventId = long.Parse(data.EventData.EventId);

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var existingEvent = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
                if (existingEvent == null) return false;

                existingEvent.Eventname = data.EventData.EventName;
                existingEvent.Startdate = data.EventData.StartDate.ToUniversalTime();
                existingEvent.Enddate = data.EventData.EndDate.ToUniversalTime();
                existingEvent.Isprivate = data.EventData.IsPrivate;
                existingEvent.Basepausetime = data.EventData.BasePauseTime;
                existingEvent.Locationpausetime = data.EventData.LocationPauseTime;
                existingEvent.Locweight = data.EventData.LocWeight;
                existingEvent.Groupweight = data.EventData.GroupWeight;
                existingEvent.Typeweight = data.EventData.TypeWeight;
                existingEvent.Compweight = data.EventData.CompWeight;

                await RemoveChildRowsAsync(eventId);
                await InsertChildRowsAsync(eventId, data);

                await tx.CommitAsync();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                return false;
            }
        }

        public async Task<bool> FillWizardEventAsync(long eventId, DataDTO data)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var existingEvent = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
                if (existingEvent == null) return false;

                existingEvent.Eventname = data.EventData.EventName;
                existingEvent.Startdate = data.EventData.StartDate.ToUniversalTime();
                existingEvent.Enddate = data.EventData.EndDate.ToUniversalTime();
                existingEvent.Isprivate = data.EventData.IsPrivate;
                existingEvent.Basepausetime = data.EventData.BasePauseTime;
                existingEvent.Locationpausetime = data.EventData.LocationPauseTime;

                await InsertChildRowsAsync(eventId, data);

                await tx.CommitAsync();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                return false;
            }
        }

        // ON DELETE CASCADE-del is kiváltható, de itt explicit maradt, mert
        // a Save útvonal a meglévő Event sort megtartja (nem törli), csak a
        // gyerek sorokat cseréli le teljesen.
        private async Task RemoveChildRowsAsync(long eventId)
        {
            _db.Eventtypes.RemoveRange(_db.Eventtypes.Where(w => w.EventId == eventId));
            _db.Groups.RemoveRange(_db.Groups.Where(w => w.EventId == eventId));
            _db.Locations.RemoveRange(_db.Locations.Where(w => w.EventId == eventId));
            _db.Locationtables.RemoveRange(_db.Locationtables.Where(w => w.EventId == eventId));
            _db.Participants.RemoveRange(_db.Participants.Where(w => w.EventId == eventId));
            _db.Pausetables.RemoveRange(_db.Pausetables.Where(w => w.EventId == eventId));
            _db.Registrations.RemoveRange(_db.Registrations.Where(w => w.EventId == eventId));
            _db.Eventconstraints.RemoveRange(_db.Eventconstraints.Where(w => w.EventId == eventId));
            await _db.SaveChangesAsync();
        }

        private async Task InsertChildRowsAsync(long eventId, DataDTO data)
        {
            var eventTypeMap = new Dictionary<string, Eventtype>();
            foreach (var item in data.EventTypes)
            {
                var temp = new Eventtype { EventId = eventId, Typename = item.TypeName, Timerange = item.TimeRange };
                _db.Eventtypes.Add(temp);
                eventTypeMap[item.EventTypeId] = temp;
            }
            await _db.SaveChangesAsync();

            var groupMap = new Dictionary<string, Group>();
            foreach (var item in data.Groups)
            {
                var temp = new Group { EventId = eventId, Groupname = item.GroupName };
                _db.Groups.Add(temp);
                groupMap[item.GroupId] = temp;
            }
            await _db.SaveChangesAsync();

            var locationMap = new Dictionary<string, Location>();
            foreach (var item in data.Locations)
            {
                var temp = new Location { Locationname = item.LocationName, EventId = eventId, Slots = item.Slots };
                _db.Locations.Add(temp);
                locationMap[item.LocationId] = temp;
            }
            await _db.SaveChangesAsync();

            foreach (var item in data.LocationTable)
            {
                _db.Locationtables.Add(new Locationtable
                {
                    EventId = eventId,
                    LocationId = locationMap[item.LocationId].LocationId,
                    EventtypeId = eventTypeMap[item.EventTypeId].EventtypeId
                });
            }
            await _db.SaveChangesAsync();

            var participantMap = new Dictionary<string, Participant>();
            foreach (var item in data.Participants)
            {
                var temp = new Participant
                {
                    Participantname = item.ParticipantName,
                    EventId = eventId,
                    Competitornumber = item.CompetitorNumber,
                    GroupId = !string.IsNullOrEmpty(item.GroupId) ? groupMap[item.GroupId].GroupId : null
                };
                _db.Participants.Add(temp);
                participantMap[item.ParticipantId] = temp;
            }
            await _db.SaveChangesAsync();

            foreach (var item in data.PauseTable)
            {
                _db.Pausetables.Add(new Pausetable
                {
                    EventId = eventId,
                    LocationId1 = locationMap[item.LocationId1].LocationId,
                    LocationId2 = locationMap[item.LocationId2].LocationId,
                    Pause = item.Pause
                });
            }
            await _db.SaveChangesAsync();

            foreach (var item in data.Registrations)
            {
                _db.Registrations.Add(new Registration
                {
                    EventId = eventId,
                    ParticipantId = participantMap[item.ParticipantId].ParticipantId,
                    EventtypeId = eventTypeMap[item.EventTypeId].EventtypeId
                });
            }
            await _db.SaveChangesAsync();

            foreach (var item in data.Constraints)
            {
                var temp = new Eventconstraint
                {
                    EventId = eventId,
                    Constrainttype = item.ConstraintType[0],
                    Starttime = item.StartTime.ToUniversalTime(),
                    Endtime = item.EndTime.ToUniversalTime()
                };
                temp.ObjectId = temp.Constrainttype switch
                {
                    'L' => locationMap[item.ObjectId].LocationId,
                    'G' => groupMap[item.ObjectId].GroupId,
                    'C' => participantMap[item.ObjectId].ParticipantId,
                    'T' => eventTypeMap[item.ObjectId].EventtypeId,
                    _ => throw new InvalidOperationException($"Unknown constraint type: {temp.Constrainttype}")
                };
                _db.Eventconstraints.Add(temp);
            }
            await _db.SaveChangesAsync();
        }
    }
}