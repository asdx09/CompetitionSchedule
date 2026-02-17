using ScheduleLogic.Server.Models;

namespace ScheduleLogic.Server.Class
{
    public class dataDTO
    {
        public EventDTO eventData { get; set; } = new EventDTO();
        public List<EventTypeDTO> eventTypes { get; set; } = []; 
        public List<GroupDTO> groups { get; set; } = [];
        public List<LocationDTO> locations { get; set; } = [];
        public List<ParticipantDTO> participants { get; set; } = [];
        public List<RegistrationDTO> registrations { get; set; } = [];
        public List<PauseTableDTO> pauseTable { get; set; } = [];
        public List<LocationTableDTO> locationTable { get; set; } = [];
        public List<ConstraintDTO> constraints { get; set; } = [];
    }
    public class EventTypeDTO
    {
        public string EventTypeId { get; set; } = "";
        public string EventId { get; set; } = "";
        public string TypeName { get; set; } = null!;
        public TimeOnly TimeRange { get; set; }
    }
    public class EventDTO
    {
        public string EventId { get; set; } = "";
        public string EventName { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string CreatedBy { get; set; } = "";
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
        public string GroupId { get; set; } = "";
        public string EventId { get; set; } = "";
        public string GroupName { get; set; } = null!;
    }
    public class LocationDTO
    {
        public string LocationId { get; set; } = "";
        public string EventId { get; set; } = "";
        public string LocationName { get; set; } = null!;
        public int Slots { get; set; }

    }
    public partial class LocationTableDTO
    {
        public string LocationTableId { get; set; } = "";
        public string EventId { get; set; } = ""; 
        public string EventTypeId { get; set; } = "";
        public string LocationId { get; set; } = "";
    }
    public partial class ParticipantDTO
    {
        public string ParticipantId { get; set; } = "";
        public int CompetitorNumber { get; set; }
        public string ParticipantName { get; set; } = null!;
        public string EventId { get; set; } = "";
        public string? GroupId { get; set; }

    }
    public partial class PauseTableDTO
    {
        public string PauseId { get; set; } = "";
        public string EventId { get; set; } = "";
        public string LocationId1 { get; set; } = "";
        public string LocationId2 { get; set; } = "";
        public TimeOnly Pause { get; set; }
    }
    public partial class RegistrationDTO
    {
        public string RegistrationId { get; set; } = "";
        public string EventId { get; set; } = "";
        public string ParticipantId { get; set; } = "";
        public string EventTypeId { get; set; } = "";

    }
    public partial class ConstraintDTO
    {
        public string ConstraintId { get; set; } = "";
        public string EventId { get; set; } = "";
        public string ObjectId { get; set; } = "";
        public string ConstraintType { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}
