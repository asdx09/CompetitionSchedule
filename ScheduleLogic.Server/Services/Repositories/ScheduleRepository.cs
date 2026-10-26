using Microsoft.EntityFrameworkCore;
using ScheduleLogic.Server.Models;
using ScheduleLogic.Server.Services.Interfaces;
using static ScheduleLogic.Server.Class.EventModels;
using static ScheduleLogic.Server.Class.ScheduleModels;

namespace ScheduleLogic.Server.Services
{
    public class ScheduleRepository : IScheduleRepository
    {
        const int MinuteConversion = 1;

        private readonly ScheduleLogicContext _db;

        public ScheduleRepository(ScheduleLogicContext db)
        {
            _db = db;
        }

        public async Task<ScheduleRequestForSolver> GetScheduleInfoAsync(long eventId)
        {
            var request = new ScheduleRequestForSolver { EventId = (int)eventId };

            var evt = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
            if (evt == null) throw new InvalidOperationException($"Event {eventId} not found.");

            request.Locations = await _db.Locations
                .Where(l => l.EventId == eventId)
                .Select(l => new LocationModel { Id = l.LocationId, Name = l.Locationname, Capacity = (int)l.Slots })
                .ToListAsync();

            var eventTypes = await _db.Eventtypes.Where(e => e.EventId == eventId).ToListAsync();
            var locationTables = await _db.Locationtables
                .Where(lt => eventTypes.Select(e => e.EventtypeId).Contains(lt.EventtypeId))
                .ToListAsync();

            request.Events = eventTypes.Select(e => new EventModel
            {
                Id = e.EventtypeId,
                Name = e.Typename,
                Duration = (e.Timerange.Hour * 60 + e.Timerange.Minute) / MinuteConversion,
                PossibleLocations = locationTables
                    .Where(lt => lt.EventtypeId == e.EventtypeId)
                    .Select(lt => lt.LocationId)
                    .ToList()
            }).ToList();

            request.Competitors = await _db.Participants
                .Where(p => p.EventId == eventId)
                .Select(p => new CompetitorModel { Id = p.ParticipantId, Name = p.Participantname, GroupId = p.GroupId ?? -1 })
                .ToListAsync();

            request.Entries = await _db.Registrations
                .Where(r => r.EventId == eventId)
                .Select(r => new EntryModel { Id = r.RegistrationId, EventId = r.EventtypeId, CompetitorId = r.ParticipantId })
                .ToListAsync();

            var constraints = await _db.Eventconstraints.Where(c => c.EventId == eventId).ToListAsync();
            request.Constraints = constraints.Select(c => new ConstraintModel
            {
                Id = c.ConstraintId,
                ConstraintType = c.Constrainttype.ToString(),
                ObjectId = c.ObjectId,
                StartTime = (int)(c.Starttime - evt.Startdate).TotalMinutes / MinuteConversion,
                EndTime = (int)(c.Endtime - evt.Startdate).TotalMinutes / MinuteConversion
            }).ToList();

            request.Travel = await _db.Pausetables
                .Where(p => p.EventId == eventId)
                .Select(p => new PauseTableModel
                {
                    Id = p.PauseId,
                    LocationID1 = p.LocationId1,
                    LocationID2 = p.LocationId2,
                    Pause = p.Pause.Hour * 60 + p.Pause.Minute / MinuteConversion
                })
                .ToListAsync();

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

        public async Task<bool> SaveSolverResultAsync(List<ScheduleModel> schedule, long eventId)
        {
            var typeIds = await _db.Eventtypes.Where(w => w.EventId == eventId).Select(s => s.EventtypeId).ToListAsync();
            var evt = await _db.Events.FirstOrDefaultAsync(w => w.EventId == eventId);
            if (evt == null) return false;

            var toRemove = await _db.Schedules.Where(w => typeIds.Contains(w.EventtypeId)).ToListAsync();
            _db.Schedules.RemoveRange(toRemove);

            foreach (var item in schedule)
            {
                _db.Schedules.Add(new Schedule
                {
                    ParticipantId = item.ParticipantId,
                    Starttime = evt.Startdate.AddMinutes(item.Start * MinuteConversion),
                    Endtime = evt.Startdate.AddMinutes(item.End * MinuteConversion),
                    LocationId = item.LocationId,
                    Slot = item.Slot,
                    EventtypeId = item.EventTypeId
                });
            }
            await _db.SaveChangesAsync();

            var schedules = await _db.Schedules.Where(w => typeIds.Contains(w.EventtypeId)).ToListAsync();
            var locations = await _db.Locations.Where(l => l.EventId == eventId).ToListAsync();

            foreach (var group in schedules.GroupBy(s => s.LocationId))
            {
                var location = locations.FirstOrDefault(l => l.LocationId == group.Key);
                if (location == null) continue;

                var activeSlots = new List<Schedule>();
                foreach (var entry in group.OrderBy(s => s.Starttime))
                {
                    activeSlots = activeSlots
                        .Where(a => a.Endtime + new TimeSpan(0, 5, 0) > entry.Starttime)
                        .ToList();

                    for (int slot = 1; slot <= location.Slots; slot++)
                    {
                        if (!activeSlots.Any(a => a.Slot == slot))
                        {
                            entry.Slot = slot;
                            activeSlots.Add(entry);
                            break;
                        }
                    }
                }
            }
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<DataDTO> GetScheduleDataAsync(long eventId)
        {
            var evt = await _db.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventId == eventId)
                ?? throw new InvalidOperationException($"Event {eventId} not found.");

            var data = new DataDTO
            {
                EventData = new EventDTO
                {
                    EventId = evt.EventId.ToString(),
                    EventName = evt.Eventname,
                    StartDate = evt.Startdate,
                    EndDate = evt.Enddate,
                    IsPrivate = evt.Isprivate,
                    BasePauseTime = evt.Basepausetime,
                    LocationPauseTime = evt.Locationpausetime,
                    LocWeight = evt.Locweight,
                    GroupWeight = evt.Groupweight,
                    TypeWeight = evt.Typeweight,
                    CompWeight = evt.Compweight
                }
            };

            data.EventTypes = await _db.Eventtypes
                .AsNoTracking()
                .Where(x => x.EventId == eventId)
                .Select(x => new EventTypeDTO
                {
                    EventTypeId = x.EventtypeId.ToString(),
                    EventId = x.EventId.ToString(),
                    TypeName = x.Typename,
                    TimeRange = x.Timerange
                })
                .ToListAsync();

            data.Groups = await _db.Groups
                .AsNoTracking()
                .Where(x => x.EventId == eventId)
                .Select(x => new GroupDTO
                {
                    GroupId = x.GroupId.ToString(),
                    EventId = x.EventId.ToString(),
                    GroupName = x.Groupname
                })
                .ToListAsync();

            data.Locations = await _db.Locations
                .AsNoTracking()
                .Where(x => x.EventId == eventId)
                .Select(x => new LocationDTO
                {
                    LocationId = x.LocationId.ToString(),
                    EventId = x.EventId.ToString(),
                    LocationName = x.Locationname,
                    Slots = x.Slots
                })
                .ToListAsync();

            data.Participants = await _db.Participants
                .AsNoTracking()
                .Where(x => x.EventId == eventId)
                .Select(x => new ParticipantDTO
                {
                    ParticipantId = x.ParticipantId.ToString(),
                    CompetitorNumber = x.Competitornumber,
                    ParticipantName = x.Participantname,
                    EventId = x.EventId.ToString(),
                    GroupId = x.GroupId.HasValue
                        ? x.GroupId.Value.ToString()
                        : null
                })
                .ToListAsync();

            data.Registrations = await _db.Registrations
                .AsNoTracking()
                .Where(x => x.EventId == eventId)
                .Select(x => new RegistrationDTO
                {
                    RegistrationId = x.RegistrationId.ToString(),
                    EventId = x.EventId.ToString(),
                    ParticipantId = x.ParticipantId.ToString(),
                    EventTypeId = x.EventtypeId.ToString()
                })
                .ToListAsync();

            data.PauseTable = await _db.Pausetables
                .AsNoTracking()
                .Where(x => x.EventId == eventId)
                .Select(x => new PauseTableDTO
                {
                    PauseId = x.PauseId.ToString(),
                    EventId = x.EventId.ToString(),
                    LocationId1 = x.LocationId1.ToString(),
                    LocationId2 = x.LocationId2.ToString(),
                    Pause = x.Pause
                })
                .ToListAsync();

            data.LocationTable = await _db.Locationtables
                .AsNoTracking()
                .Where(x => x.EventId == eventId)
                .Select(x => new LocationTableDTO
                {
                    LocationTableId = x.LocationtableId.ToString(),
                    EventId = x.EventId.ToString(),
                    EventTypeId = x.EventtypeId.ToString(),
                    LocationId = x.LocationId.ToString()
                })
                .ToListAsync();

            data.Constraints = await _db.Eventconstraints
                .AsNoTracking()
                .Where(x => x.EventId == eventId)
                .Select(x => new ConstraintDTO
                {
                    ConstraintId = x.ConstraintId.ToString(),
                    EventId = x.EventId.ToString(),
                    ObjectId = x.ObjectId.ToString(),
                    ConstraintType = x.Constrainttype.ToString(),
                    StartTime = x.Starttime,
                    EndTime = x.Endtime
                })
                .ToListAsync();

            var typeIds = await _db.Eventtypes
                .Where(x => x.EventId == eventId)
                .Select(x => x.EventtypeId)
                .ToListAsync();

            var startDate = evt.Startdate.ToLocalTime();

            data.TimeZones = await _db.Schedules
                .AsNoTracking()
                .Where(x => typeIds.Contains(x.EventtypeId))
                .Select(x => new TimeZoneDTO
                {
                    ScheduleId = x.ScheduleId,
                    EventTypeId = x.EventtypeId,
                    ParticipantId = x.ParticipantId,
                    LocationId = x.LocationId,
                    StartTime = Convert.ToInt32(
                        (x.Starttime.ToLocalTime() - startDate.Date).TotalMinutes),
                    EndTime = Convert.ToInt32(
                        (x.Endtime.ToLocalTime() - startDate.Date).TotalMinutes),
                    Slot = x.Slot
                })
                .ToListAsync();

            return data;
        }

        public async Task<ScheduleDataForEXPORT?> GetExportDataAsync(long eventId)
        {
            var evt = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
            if (evt == null) return null;

            var typeIds = await _db.Eventtypes.Where(e => e.EventId == eventId).Select(e => e.EventtypeId).ToListAsync();
            var eventTypes = await _db.Eventtypes.Where(e => e.EventId == eventId).ToListAsync();
            var participants = await _db.Participants.Where(p => p.EventId == eventId).ToListAsync();
            var locations = await _db.Locations.Where(l => l.EventId == eventId).ToListAsync();
            var groups = await _db.Groups.Where(g => g.EventId == eventId).ToListAsync();
            var schedules = await _db.Schedules.Where(s => typeIds.Contains(s.EventtypeId)).ToListAsync();

            var eventTypeDict = eventTypes.ToDictionary(e => e.EventtypeId, e => e.Typename);
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
                    StartTime = item.Starttime.ToLocalTime(),
                    EndTime = item.Endtime.ToLocalTime(),
                    Slot = item.Slot,
                    GroupName = groupName
                };
            }).ToList();

            return new ScheduleDataForEXPORT
            {
                EventName = evt.Eventname,
                StartDate = evt.Startdate.ToLocalTime(),
                EndDate = evt.Enddate.ToLocalTime(),
                TimeZones = timeZones
            };
        }
    }
}