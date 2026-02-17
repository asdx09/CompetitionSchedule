using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ScheduleLogic.Server.Models;

public partial class ScheduleLogicDbContext : DbContext
{
    public ScheduleLogicDbContext()
    {
    }

    public ScheduleLogicDbContext(DbContextOptions<ScheduleLogicDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Constraint> Constraints { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<EventType> EventTypes { get; set; }

    public virtual DbSet<Group> Groups { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<LocationTable> LocationTables { get; set; }

    public virtual DbSet<Log> Logs { get; set; }

    public virtual DbSet<Participant> Participants { get; set; }

    public virtual DbSet<PauseTable> PauseTables { get; set; }

    public virtual DbSet<Registration> Registrations { get; set; }

    public virtual DbSet<Schedule> Schedules { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=ScheduleLogicDB;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Constraint>(entity =>
        {
            entity.HasKey(e => e.ConstraintId).HasName("constraint_constraint_id_primary");

            entity.ToTable("Constraint");

            entity.Property(e => e.ConstraintId).HasColumnName("Constraint_ID");
            entity.Property(e => e.ConstraintType)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.EventId).HasColumnName("Event_ID");
            entity.Property(e => e.ObjectId).HasColumnName("Object_ID");
            entity.Property(e => e.StartTime).HasColumnType("datetime");

            entity.HasOne(d => d.Event).WithMany(p => p.Constraints)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("constraint_event_id_foreign");
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.EventId).HasName("event_event_id_primary");

            entity.ToTable("Event");

            entity.Property(e => e.EventId).HasColumnName("Event_ID");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.EventName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.StartDate).HasColumnType("datetime");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Events)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("event_createdby_foreign");
        });

        modelBuilder.Entity<EventType>(entity =>
        {
            entity.HasKey(e => e.EventTypeId).HasName("eventtype_eventtype_id_primary");

            entity.ToTable("EventType");

            entity.Property(e => e.EventTypeId).HasColumnName("EventType_ID");
            entity.Property(e => e.EventId).HasColumnName("Event_ID");
            entity.Property(e => e.TypeName)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.Event).WithMany(p => p.EventTypes)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("eventtype_event_id_foreign");
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.GroupId).HasName("group_group_id_primary");

            entity.ToTable("Group");

            entity.Property(e => e.GroupId).HasColumnName("Group_ID");
            entity.Property(e => e.EventId).HasColumnName("Event_ID");
            entity.Property(e => e.GroupName)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.Event).WithMany(p => p.Groups)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("group_event_id_foreign");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("location_location_id_primary");

            entity.ToTable("Location");

            entity.Property(e => e.LocationId).HasColumnName("Location_ID");
            entity.Property(e => e.EventId).HasColumnName("Event_ID");
            entity.Property(e => e.LocationName)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.Event).WithMany(p => p.Locations)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("location_event_id_foreign");
        });

        modelBuilder.Entity<LocationTable>(entity =>
        {
            entity.HasKey(e => e.LocationTableId).HasName("locationtable_locationtable_id_primary");

            entity.ToTable("LocationTable");

            entity.Property(e => e.LocationTableId).HasColumnName("LocationTable_ID");
            entity.Property(e => e.EventId).HasColumnName("Event_ID");
            entity.Property(e => e.EventTypeId).HasColumnName("EventType_ID");
            entity.Property(e => e.LocationId).HasColumnName("Location_ID");

            entity.HasOne(d => d.Event).WithMany(p => p.LocationTables)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("locationtable_event_id_foreign");

            entity.HasOne(d => d.EventType).WithMany(p => p.LocationTables)
                .HasForeignKey(d => d.EventTypeId)
                .HasConstraintName("locationtable_eventtype_id_foreign");

            entity.HasOne(d => d.Location).WithMany(p => p.LocationTables)
                .HasForeignKey(d => d.LocationId)
                .HasConstraintName("locationtable_location_id_foreign");
        });

        modelBuilder.Entity<Log>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("log_log_id_primary");

            entity.ToTable("Log");

            entity.Property(e => e.LogId).HasColumnName("Log_ID");
            entity.Property(e => e.LogDate).HasColumnType("datetime");
            entity.Property(e => e.LogText)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserId).HasColumnName("User_ID");

            entity.HasOne(d => d.User).WithMany(p => p.Logs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("log_user_id_foreign");
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasKey(e => e.ParticipantId).HasName("participant_participant_id_primary");

            entity.ToTable("Participant");

            entity.Property(e => e.ParticipantId).HasColumnName("Participant_ID");
            entity.Property(e => e.EventId).HasColumnName("Event_ID");
            entity.Property(e => e.GroupId).HasColumnName("Group_ID");
            entity.Property(e => e.ParticipantName)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.Event).WithMany(p => p.Participants)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("participant_event_id_foreign");

            entity.HasOne(d => d.Group).WithMany(p => p.Participants)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("participant_group_id_foreign");
        });

        modelBuilder.Entity<PauseTable>(entity =>
        {
            entity.HasKey(e => e.PauseId).HasName("pausetable_pause_id_primary");

            entity.ToTable("PauseTable");

            entity.Property(e => e.PauseId).HasColumnName("Pause_ID");
            entity.Property(e => e.EventId).HasColumnName("Event_ID");
            entity.Property(e => e.LocationId1).HasColumnName("Location_ID1");
            entity.Property(e => e.LocationId2).HasColumnName("Location_ID2");

            entity.HasOne(d => d.Event).WithMany(p => p.PauseTables)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("pausetable_event_id_foreign");

            entity.HasOne(d => d.LocationId1Navigation).WithMany(p => p.PauseTableLocationId1Navigations)
                .HasForeignKey(d => d.LocationId1)
                .HasConstraintName("pausetable_location_id1_foreign");

            entity.HasOne(d => d.LocationId2Navigation).WithMany(p => p.PauseTableLocationId2Navigations)
                .HasForeignKey(d => d.LocationId2)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pausetable_location_id2_foreign");
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(e => e.RegistrationId).HasName("registrations_registration_id_primary");

            entity.Property(e => e.RegistrationId).HasColumnName("Registration_ID");
            entity.Property(e => e.EventId).HasColumnName("Event_ID");
            entity.Property(e => e.EventTypeId).HasColumnName("EventType_ID");
            entity.Property(e => e.ParticipantId).HasColumnName("Participant_ID");

            entity.HasOne(d => d.Event).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("registrations_event_id_foreign");

            entity.HasOne(d => d.EventType).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.EventTypeId)
                .HasConstraintName("registrations_eventtype_id_foreign");

            entity.HasOne(d => d.Participant).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.ParticipantId)
                .HasConstraintName("registrations_participant_id_foreign");
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId).HasName("schedule_schedule_id_primary");

            entity.ToTable("Schedule");

            entity.Property(e => e.ScheduleId).HasColumnName("Schedule_ID");
            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.EventTypeId).HasColumnName("EventType_ID");
            entity.Property(e => e.LocationId).HasColumnName("Location_ID");
            entity.Property(e => e.ParticipantId).HasColumnName("Participant_ID");
            entity.Property(e => e.StartTime).HasColumnType("datetime");

            entity.HasOne(d => d.EventType).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.EventTypeId)
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
            entity.HasKey(e => e.UserId).HasName("users_user_id_primary");

            entity.Property(e => e.UserId).HasColumnName("User_ID");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
