using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Constraint
{
    public long ConstraintId { get; set; }

    public long EventId { get; set; }

    public long ObjectId { get; set; }

    public string ConstraintType { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public virtual Event Event { get; set; } = null!;
}
