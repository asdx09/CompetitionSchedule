using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Log
{
    public long LogId { get; set; }

    public long UserId { get; set; }

    public string Logtext { get; set; } = null!;

    public DateTime Logdate { get; set; }

    public virtual User User { get; set; } = null!;
}
