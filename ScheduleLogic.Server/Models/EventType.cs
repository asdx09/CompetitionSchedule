using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class EventType
{
    public long EventTypeId { get; set; }

    public long EventId { get; set; }

    public string TypeName { get; set; } = null!;

    public TimeOnly TimeRange { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual ICollection<LocationTable> LocationTables { get; set; } = new List<LocationTable>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
