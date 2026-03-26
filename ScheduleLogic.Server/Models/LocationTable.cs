using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Locationtable
{
    public long LocationtableId { get; set; }

    public long EventId { get; set; }

    public long EventtypeId { get; set; }

    public long LocationId { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual Eventtype Eventtype { get; set; } = null!;

    public virtual Location Location { get; set; } = null!;
}
