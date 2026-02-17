using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class LocationTable
{
    public long LocationTableId { get; set; }

    public long EventId { get; set; }

    public long EventTypeId { get; set; }

    public long LocationId { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual EventType EventType { get; set; } = null!;

    public virtual Location Location { get; set; } = null!;
}
