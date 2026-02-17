using AutoMapper;
using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Models;

public class AutoMappers : Profile
{
    public AutoMappers()
    {
        CreateMap<Event, EventDTO>();
        CreateMap<EventType, EventTypeDTO>();
        CreateMap<Group, GroupDTO>();
        CreateMap<Location, LocationDTO>();
        CreateMap<Participant, ParticipantDTO>();
        CreateMap<PauseTable, PauseTableDTO>();
        CreateMap<Registration, RegistrationDTO>();
        CreateMap<LocationTable, LocationTableDTO>();
        CreateMap<Constraint, ConstraintDTO>();
    }
}
