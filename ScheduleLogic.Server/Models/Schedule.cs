using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Schedule
{
    public long ScheduleId { get; set; }

    public long EventtypeId { get; set; }

    public long ParticipantId { get; set; }

    public long LocationId { get; set; }

    public DateTime Starttime { get; set; }

    public DateTime Endtime { get; set; }

    public int Slot { get; set; }

    public virtual Eventtype Eventtype { get; set; } = null!;

    public virtual Location Location { get; set; } = null!;

    public virtual Participant Participant { get; set; } = null!;
}
