using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Location
{
    public long LocationId { get; set; }

    public long EventId { get; set; }

    public string LocationName { get; set; } = null!;

    public int Slots { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual ICollection<LocationTable> LocationTables { get; set; } = new List<LocationTable>();

    public virtual ICollection<PauseTable> PauseTableLocationId1Navigations { get; set; } = new List<PauseTable>();

    public virtual ICollection<PauseTable> PauseTableLocationId2Navigations { get; set; } = new List<PauseTable>();

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
