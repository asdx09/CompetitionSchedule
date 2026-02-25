namespace ScheduleLogic.Server.Class
{
    public class EventModels
    {
        public class EventsData
        {
            public string EventName { get; set; } = null!;
            public long EventId { get; set; }
        }

        public class DataDTO
        {
            public EventDTO EventData { get; set; } = new EventDTO();
            public List<EventTypeDTO> EventTypes { get; set; } = new List<EventTypeDTO>();
            public List<GroupDTO> Groups { get; set; } = new List<GroupDTO>();
            public List<LocationDTO> Locations { get; set; } = new List<LocationDTO>();
            public List<ParticipantDTO> Participants { get; set; } = new List<ParticipantDTO>();
            public List<RegistrationDTO> Registrations { get; set; } = new List<RegistrationDTO>();
            public List<PauseTableDTO> PauseTable { get; set; } = new List<PauseTableDTO>();
            public List<LocationTableDTO> LocationTable { get; set; } = new List<LocationTableDTO>();
            public List<ConstraintDTO> Constraints { get; set; } = new List<ConstraintDTO>();
            public List<TimeZoneDTO> TimeZones { get; set; } = new List<TimeZoneDTO>();
        }
        public class EventTypeDTO
        {
            public string EventTypeId { get; set; } = null!;
            public string EventId { get; set; } = null!;
            public string TypeName { get; set; } = null!;
            public TimeOnly TimeRange { get; set; }
        }
        public class EventDTO
        {
            public string EventId { get; set; } = null!;
            public string EventName { get; set; } = null!;
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public bool IsPrivate { get; set; }
            public int BasePauseTime { get; set; }
            public int LocationPauseTime { get; set; }
            public int LocWeight { get; set; }
            public int GroupWeight { get; set; }
            public int TypeWeight { get; set; }
            public int CompWeight { get; set; }
        }
        public class GroupDTO
        {
            public string GroupId { get; set; } = null!;
            public string EventId { get; set; } = null!;
            public string GroupName { get; set; } = null!;
        }
        public class LocationDTO
        {
            public string LocationId { get; set; } = null!;
            public string EventId { get; set; } = null!;
            public string LocationName { get; set; } = null!;
            public int Slots { get; set; }

        }
        public class LocationTableDTO
        {
            public string LocationTableId { get; set; } = null!;
            public string EventId { get; set; } = null!;
            public string EventTypeId { get; set; } = null!;
            public string LocationId { get; set; } = null!;
        }
        public class ParticipantDTO
        {
            public string ParticipantId { get; set; } = null!;
            public int CompetitorNumber { get; set; }
            public string ParticipantName { get; set; } = null!;
            public string EventId { get; set; } = null!;
            public string? GroupId { get; set; }

        }
        public class PauseTableDTO
        {
            public string PauseId { get; set; } = null!;
            public string EventId { get; set; } = null!;
            public string LocationId1 { get; set; } = null!;
            public string LocationId2 { get; set; } = null!;
            public TimeOnly Pause { get; set; }
        }
        public class RegistrationDTO
        {
            public string RegistrationId { get; set; } = null!;
            public string EventId { get; set; } = null!;
            public string ParticipantId { get; set; } = null!;
            public string EventTypeId { get; set; } = null!;

        }
        public class ConstraintDTO
        {
            public string ConstraintId { get; set; } = null!;
            public string EventId { get; set; } = null!;
            public string ObjectId { get; set; } = null!;
            public string ConstraintType { get; set; } = null!;
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }
        public class TimeZoneDTO
        {
            public long ScheduleId { get; set; }
            public long EventTypeId { get; set; }
            public long ParticipantId { get; set; }
            public long LocationId { get; set; }
            public int StartTime { get; set; }
            public int EndTime { get; set; }
            public int Slot { get; set; }
        }
    }
}
