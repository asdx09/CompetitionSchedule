using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class PauseTable
{
    public long PauseId { get; set; }

    public long EventId { get; set; }

    public long LocationId1 { get; set; }

    public long LocationId2 { get; set; }

    public TimeOnly Pause { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual Location LocationId1Navigation { get; set; } = null!;

    public virtual Location LocationId2Navigation { get; set; } = null!;
}
