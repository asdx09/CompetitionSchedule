using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Eventtype
{
    public long EventtypeId { get; set; }

    public long EventId { get; set; }

    public string Typename { get; set; } = null!;

    public TimeOnly Timerange { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual ICollection<Locationtable> Locationtables { get; set; } = new List<Locationtable>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
