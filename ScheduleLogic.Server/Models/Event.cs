using System;
using System.Collections.Generic;

namespace ScheduleLogic.Server.Models;

public partial class Event
{
    public long EventId { get; set; }

    public string Eventname { get; set; } = null!;

    public DateTime Startdate { get; set; }

    public DateTime Enddate { get; set; }

    public long? Createdby { get; set; }

    public bool Isprivate { get; set; }

    public int Locationpausetime { get; set; }

    public int Basepausetime { get; set; }

    public int Locweight { get; set; }

    public int Typeweight { get; set; }

    public int Compweight { get; set; }

    public int Groupweight { get; set; }

    public virtual User? CreatedbyNavigation { get; set; }

    public virtual ICollection<Eventconstraint> Eventconstraints { get; set; } = new List<Eventconstraint>();

    public virtual ICollection<Eventtype> Eventtypes { get; set; } = new List<Eventtype>();

    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();

    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();

    public virtual ICollection<Locationtable> Locationtables { get; set; } = new List<Locationtable>();

    public virtual ICollection<Participant> Participants { get; set; } = new List<Participant>();

    public virtual ICollection<Pausetable> Pausetables { get; set; } = new List<Pausetable>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
