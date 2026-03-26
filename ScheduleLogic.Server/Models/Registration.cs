using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Registration
{
    public long RegistrationId { get; set; }

    public long EventId { get; set; }

    public long ParticipantId { get; set; }

    public long EventtypeId { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual Eventtype Eventtype { get; set; } = null!;

    public virtual Participant Participant { get; set; } = null!;
}
