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
        => optionsBuilder.UseNpgsql("Host=localhost;Database=scheduleLogic;Username=app;Password=app");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.EventId).HasName("event_pkey");

            entity.ToTable("event", "public");

            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.Basepausetime).HasColumnName("basepausetime");
            entity.Property(e => e.Compweight).HasColumnName("compweight");
            entity.Property(e => e.Createdby).HasColumnName("createdby");
            entity.Property(e => e.Enddate).HasColumnName("enddate");
            entity.Property(e => e.Eventname)
                .HasMaxLength(255)
                .HasColumnName("eventname");
            entity.Property(e => e.Groupweight).HasColumnName("groupweight");
            entity.Property(e => e.Isprivate).HasColumnName("isprivate");
            entity.Property(e => e.Locationpausetime).HasColumnName("locationpausetime");
            entity.Property(e => e.Locweight).HasColumnName("locweight");
            entity.Property(e => e.Startdate).HasColumnName("startdate");
            entity.Property(e => e.Typeweight).HasColumnName("typeweight");

            entity.HasOne(d => d.CreatedbyNavigation).WithMany(p => p.Events)
                .HasForeignKey(d => d.Createdby)
                .HasConstraintName("event_createdby_foreign");
        });

        modelBuilder.Entity<Eventconstraint>(entity =>
        {
            entity.HasKey(e => e.ConstraintId).HasName("eventconstraint_pkey");

            entity.ToTable("eventconstraint", "public");

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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("constraint_event_id_foreign");
        });

        modelBuilder.Entity<Eventtype>(entity =>
        {
            entity.HasKey(e => e.EventtypeId).HasName("eventtype_pkey");

            entity.ToTable("eventtype", "public");

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

            entity.ToTable("groups", "public");

            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");

            entity.HasOne(d => d.Event).WithMany(p => p.Groups)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("group_event_id_foreign");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("location_pkey");

            entity.ToTable("location", "public");

            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.Locationname)
                .HasMaxLength(255)
                .HasColumnName("locationname");
            entity.Property(e => e.Slots).HasColumnName("slots");

            entity.HasOne(d => d.Event).WithMany(p => p.Locations)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("location_event_id_foreign");
        });

        modelBuilder.Entity<Locationtable>(entity =>
        {
            entity.HasKey(e => e.LocationtableId).HasName("locationtable_pkey");

            entity.ToTable("locationtable", "public");

            entity.Property(e => e.LocationtableId).HasColumnName("locationtable_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.EventtypeId).HasColumnName("eventtype_id");
            entity.Property(e => e.LocationId).HasColumnName("location_id");

            entity.HasOne(d => d.Event).WithMany(p => p.Locationtables)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
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

            entity.ToTable("log", "public");

            entity.Property(e => e.LogId).HasColumnName("log_id");
            entity.Property(e => e.Logdate).HasColumnName("logdate");
            entity.Property(e => e.Logtext)
                .HasMaxLength(255)
                .HasColumnName("logtext");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Logs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("log_user_id_foreign");
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasKey(e => e.ParticipantId).HasName("participant_pkey");

            entity.ToTable("participant", "public");

            entity.Property(e => e.ParticipantId).HasColumnName("participant_id");
            entity.Property(e => e.Competitornumber).HasColumnName("competitornumber");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.Participantname)
                .HasMaxLength(255)
                .HasColumnName("participantname");

            entity.HasOne(d => d.Event).WithMany(p => p.Participants)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("participant_event_id_foreign");

            entity.HasOne(d => d.Group).WithMany(p => p.Participants)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("participant_group_id_foreign");
        });

        modelBuilder.Entity<Pausetable>(entity =>
        {
            entity.HasKey(e => e.PauseId).HasName("pausetable_pkey");

            entity.ToTable("pausetable", "public");

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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pausetable_location_id2_foreign");
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(e => e.RegistrationId).HasName("registrations_pkey");

            entity.ToTable("registrations", "public");

            entity.Property(e => e.RegistrationId).HasColumnName("registration_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.EventtypeId).HasColumnName("eventtype_id");
            entity.Property(e => e.ParticipantId).HasColumnName("participant_id");

            entity.HasOne(d => d.Event).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
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

            entity.ToTable("schedule", "public");

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

            entity.ToTable("users", "public");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.Registrationdate).HasColumnName("registrationdate");
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .HasColumnName("username");
            entity.Property(e => e.Validated).HasColumnName("validated");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
