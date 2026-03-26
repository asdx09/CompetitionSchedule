using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Location
{
    public long LocationId { get; set; }

    public long EventId { get; set; }

    public string Locationname { get; set; } = null!;

    public int Slots { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual ICollection<Locationtable> Locationtables { get; set; } = new List<Locationtable>();

    public virtual ICollection<Pausetable> PausetableLocationId1Navigations { get; set; } = new List<Pausetable>();

    public virtual ICollection<Pausetable> PausetableLocationId2Navigations { get; set; } = new List<Pausetable>();

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
