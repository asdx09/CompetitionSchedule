using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Schedule
{
    public long ScheduleId { get; set; }

    public long EventTypeId { get; set; }

    public long ParticipantId { get; set; }

    public long LocationId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int Slot { get; set; }

    public virtual EventType EventType { get; set; } = null!;

    public virtual Location Location { get; set; } = null!;

    public virtual Participant Participant { get; set; } = null!;
}
