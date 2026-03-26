using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Eventconstraint
{
    public long ConstraintId { get; set; }

    public long EventId { get; set; }

    public long ObjectId { get; set; }

    public char Constrainttype { get; set; }

    public DateTime Starttime { get; set; }

    public DateTime Endtime { get; set; }

    public virtual Event Event { get; set; } = null!;
}
