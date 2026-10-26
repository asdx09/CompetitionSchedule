using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ScheduleLogic.Server.Models;

public partial class ScheduleLogicContext : DbContext
{
    public ScheduleLogicContext()
    {
    }

    public ScheduleLogicContext(DbContextOptions<ScheduleLogicContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<Eventconstraint> Eventconstraints { get; set; }

    public virtual DbSet<Eventtype> Eventtypes { get; set; }

    public virtual DbSet<Group> Groups { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<Locationtable> Locationtables { get; set; }

    public virtual DbSet<Log> Logs { get; set; }

    public virtual DbSet<Participant> Participants { get; set; }

    public virtual DbSet<Pausetable> Pausetables { get; set; }

    public virtual DbSet<Registration> Registrations { get; set; }

    public virtual DbSet<Schedule> Schedules { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Database=scheduleLogic;Username=schedulelogic_app;Password=K7mQ2vL9xP4a");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.EventId).HasName("event_pkey");

            entity.ToTable("event");

            entity.HasIndex(e => e.Createdby, "event_createdby_idx");

            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.Basepausetime)
                .HasDefaultValue(5)
                .HasColumnName("basepausetime");
            entity.Property(e => e.Compweight)
                .HasDefaultValue(1)
                .HasColumnName("compweight");
            entity.Property(e => e.Createdby).HasColumnName("createdby");
            entity.Property(e => e.Enddate).HasColumnName("enddate");
            entity.Property(e => e.Eventname)
                .HasMaxLength(255)
                .HasColumnName("eventname");
            entity.Property(e => e.Groupweight)
                .HasDefaultValue(1)
                .HasColumnName("groupweight");
            entity.Property(e => e.Isprivate)
                .HasDefaultValue(true)
                .HasColumnName("isprivate");
            entity.Property(e => e.Locationpausetime)
                .HasDefaultValue(5)
                .HasColumnName("locationpausetime");
            entity.Property(e => e.Locweight)
                .HasDefaultValue(1)
                .HasColumnName("locweight");
            entity.Property(e => e.Startdate).HasColumnName("startdate");
            entity.Property(e => e.Typeweight)
                .HasDefaultValue(1)
                .HasColumnName("typeweight");

            entity.HasOne(d => d.CreatedbyNavigation).WithMany(p => p.Events)
                .HasForeignKey(d => d.Createdby)
                .HasConstraintName("event_createdby_foreign");
        });

        modelBuilder.Entity<Eventconstraint>(entity =>
        {
            entity.HasKey(e => e.ConstraintId).HasName("eventconstraint_pkey");

            entity.ToTable("eventconstraint");

            entity.HasIndex(e => e.EventId, "eventconstraint_event_id_idx");

            entity.Property(e => e.ConstraintId).HasColumnName("constraint_id");
            entity.Property(e => e.Constrainttype)
                .HasMaxLength(1)
                .HasColumnName("constrainttype");
            entity.Property(e => e.Endtime).HasColumnName("endtime");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.ObjectId).HasColumnName("object_id");
            entity.Property(e => e.Starttime).HasColumnName("starttime");

            entity.HasOne(d => d.Event).WithMany(p => p.Eventconstraints)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("constraint_event_id_foreign");
        });

        modelBuilder.Entity<Eventtype>(entity =>
        {
            entity.HasKey(e => e.EventtypeId).HasName("eventtype_pkey");

            entity.ToTable("eventtype");

            entity.HasIndex(e => e.EventId, "eventtype_event_id_idx");

            entity.Property(e => e.EventtypeId).HasColumnName("eventtype_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.Timerange).HasColumnName("timerange");
            entity.Property(e => e.Typename)
                .HasMaxLength(255)
                .HasColumnName("typename");

            entity.HasOne(d => d.Event).WithMany(p => p.Eventtypes)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("eventtype_event_id_foreign");
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.GroupId).HasName("groups_pkey");

            entity.ToTable("groups");

            entity.HasIndex(e => e.EventId, "groups_event_id_idx");

            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");

            entity.HasOne(d => d.Event).WithMany(p => p.Groups)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("group_event_id_foreign");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("location_pkey");

            entity.ToTable("location");

            entity.HasIndex(e => e.EventId, "location_event_id_idx");

            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.Locationname)
                .HasMaxLength(255)
                .HasColumnName("locationname");
            entity.Property(e => e.Slots).HasColumnName("slots");

            entity.HasOne(d => d.Event).WithMany(p => p.Locations)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("location_event_id_foreign");
        });

        modelBuilder.Entity<Locationtable>(entity =>
        {
            entity.HasKey(e => e.LocationtableId).HasName("locationtable_pkey");

            entity.ToTable("locationtable");

            entity.HasIndex(e => e.EventId, "locationtable_event_id_idx");

            entity.HasIndex(e => new { e.EventtypeId, e.LocationId }, "locationtable_eventtype_location_key").IsUnique();

            entity.HasIndex(e => e.LocationId, "locationtable_location_id_idx");

            entity.Property(e => e.LocationtableId).HasColumnName("locationtable_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.EventtypeId).HasColumnName("eventtype_id");
            entity.Property(e => e.LocationId).HasColumnName("location_id");

            entity.HasOne(d => d.Event).WithMany(p => p.Locationtables)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("locationtable_event_id_foreign");

            entity.HasOne(d => d.Eventtype).WithMany(p => p.Locationtables)
                .HasForeignKey(d => d.EventtypeId)
                .HasConstraintName("locationtable_eventtype_id_foreign");

            entity.HasOne(d => d.Location).WithMany(p => p.Locationtables)
                .HasForeignKey(d => d.LocationId)
                .HasConstraintName("locationtable_location_id_foreign");
        });

        modelBuilder.Entity<Log>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("log_pkey");

            entity.ToTable("log");

            entity.HasIndex(e => e.UserId, "log_user_id_idx");

            entity.Property(e => e.LogId).HasColumnName("log_id");
            entity.Property(e => e.Logdate)
                .HasDefaultValueSql("now()")
                .HasColumnName("logdate");
            entity.Property(e => e.Logtext)
                .HasMaxLength(255)
                .HasColumnName("logtext");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Logs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("log_user_id_foreign");
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasKey(e => e.ParticipantId).HasName("participant_pkey");

            entity.ToTable("participant");

            entity.HasIndex(e => e.EventId, "participant_event_id_idx");

            entity.HasIndex(e => e.GroupId, "participant_group_id_idx");

            entity.Property(e => e.ParticipantId).HasColumnName("participant_id");
            entity.Property(e => e.Competitornumber).HasColumnName("competitornumber");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.Participantname)
                .HasMaxLength(255)
                .HasColumnName("participantname");

            entity.HasOne(d => d.Event).WithMany(p => p.Participants)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("participant_event_id_foreign");

            entity.HasOne(d => d.Group).WithMany(p => p.Participants)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("participant_group_id_foreign");
        });

        modelBuilder.Entity<Pausetable>(entity =>
        {
            entity.HasKey(e => e.PauseId).HasName("pausetable_pkey");

            entity.ToTable("pausetable");

            entity.HasIndex(e => e.EventId, "pausetable_event_id_idx");

            entity.HasIndex(e => e.LocationId1, "pausetable_location_id1_idx");

            entity.HasIndex(e => e.LocationId2, "pausetable_location_id2_idx");

            entity.Property(e => e.PauseId).HasColumnName("pause_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.LocationId1).HasColumnName("location_id1");
            entity.Property(e => e.LocationId2).HasColumnName("location_id2");
            entity.Property(e => e.Pause).HasColumnName("pause");

            entity.HasOne(d => d.Event).WithMany(p => p.Pausetables)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("pausetable_event_id_foreign");

            entity.HasOne(d => d.LocationId1Navigation).WithMany(p => p.PausetableLocationId1Navigations)
                .HasForeignKey(d => d.LocationId1)
                .HasConstraintName("pausetable_location_id1_foreign");

            entity.HasOne(d => d.LocationId2Navigation).WithMany(p => p.PausetableLocationId2Navigations)
                .HasForeignKey(d => d.LocationId2)
                .HasConstraintName("pausetable_location_id2_foreign");
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(e => e.RegistrationId).HasName("registrations_pkey");

            entity.ToTable("registrations");

            entity.HasIndex(e => e.EventId, "registrations_event_id_idx");

            entity.HasIndex(e => e.EventtypeId, "registrations_eventtype_id_idx");

            entity.HasIndex(e => new { e.ParticipantId, e.EventtypeId }, "registrations_participant_eventtype_key").IsUnique();

            entity.Property(e => e.RegistrationId).HasColumnName("registration_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.EventtypeId).HasColumnName("eventtype_id");
            entity.Property(e => e.ParticipantId).HasColumnName("participant_id");

            entity.HasOne(d => d.Event).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("registrations_event_id_foreign");

            entity.HasOne(d => d.Eventtype).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.EventtypeId)
                .HasConstraintName("registrations_eventtype_id_foreign");

            entity.HasOne(d => d.Participant).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.ParticipantId)
                .HasConstraintName("registrations_participant_id_foreign");
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId).HasName("schedule_pkey");

            entity.ToTable("schedule");

            entity.HasIndex(e => e.EventtypeId, "schedule_eventtype_id_idx");

            entity.HasIndex(e => e.LocationId, "schedule_location_id_idx");

            entity.HasIndex(e => e.ParticipantId, "schedule_participant_id_idx");

            entity.Property(e => e.ScheduleId).HasColumnName("schedule_id");
            entity.Property(e => e.Endtime).HasColumnName("endtime");
            entity.Property(e => e.EventtypeId).HasColumnName("eventtype_id");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.ParticipantId).HasColumnName("participant_id");
            entity.Property(e => e.Slot).HasColumnName("slot");
            entity.Property(e => e.Starttime).HasColumnName("starttime");

            entity.HasOne(d => d.Eventtype).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.EventtypeId)
                .HasConstraintName("schedule_eventtype_id_foreign");

            entity.HasOne(d => d.Location).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.LocationId)
                .HasConstraintName("schedule_location_id_foreign");

            entity.HasOne(d => d.Participant).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.ParticipantId)
                .HasConstraintName("schedule_participant_id_foreign");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.Passwordresettoken, "users_passwordresettoken_idx").HasFilter("(passwordresettoken IS NOT NULL)");

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.HasIndex(e => e.Validated, "users_validated_token_idx")
                .IsUnique()
                .HasFilter("((validated)::text <> ''::text)");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Email)
                .HasColumnType("citext")
                .HasColumnName("email");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.Passwordresetexpires).HasColumnName("passwordresetexpires");
            entity.Property(e => e.Passwordresettoken)
                .HasMaxLength(255)
                .HasColumnName("passwordresettoken");
            entity.Property(e => e.Registrationdate)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnName("registrationdate");
            entity.Property(e => e.Username)
                .HasColumnType("citext")
                .HasColumnName("username");
            entity.Property(e => e.Validated)
                .HasMaxLength(255)
                .HasColumnName("validated");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
