using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Event
{
    public long EventId { get; set; }

    public string EventName { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public long? CreatedBy { get; set; }

    public bool IsPrivate { get; set; }

    public int LocationPauseTime { get; set; }

    public int BasePauseTime { get; set; }

    public int LocWeight { get; set; }

    public int TypeWeight { get; set; }

    public int CompWeight { get; set; }

    public int GroupWeight { get; set; }

    public virtual ICollection<Constraint> Constraints { get; set; } = new List<Constraint>();

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<EventType> EventTypes { get; set; } = new List<EventType>();

    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();

    public virtual ICollection<LocationTable> LocationTables { get; set; } = new List<LocationTable>();

    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();

    public virtual ICollection<Participant> Participants { get; set; } = new List<Participant>();

    public virtual ICollection<PauseTable> PauseTables { get; set; } = new List<PauseTable>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
