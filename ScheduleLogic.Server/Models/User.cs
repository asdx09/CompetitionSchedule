using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class User
{
    public long UserId { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public bool Validated { get; set; }

    public DateOnly RegistrationDate { get; set; }

    public virtual ICollection<Event> Events { get; set; } = new List<Event>();

    public virtual ICollection<Log> Logs { get; set; } = new List<Log>();
}
