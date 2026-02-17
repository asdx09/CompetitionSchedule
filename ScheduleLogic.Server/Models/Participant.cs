using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Participant
{
    public long ParticipantId { get; set; }

    public int CompetitorNumber { get; set; }

    public string ParticipantName { get; set; } = null!;

    public long EventId { get; set; }

    public long? GroupId { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual Group? Group { get; set; }

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
