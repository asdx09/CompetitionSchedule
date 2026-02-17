using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Group
{
    public long GroupId { get; set; }

    public long EventId { get; set; }

    public string GroupName { get; set; } = null!;

    public virtual Event Event { get; set; } = null!;

    public virtual ICollection<Participant> Participants { get; set; } = new List<Participant>();
}
