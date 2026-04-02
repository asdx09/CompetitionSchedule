DROP SCHEMA public CASCADE;
CREATE SCHEMA public;

CREATE TABLE users (
    user_id BIGSERIAL PRIMARY KEY,
    username VARCHAR(255) NOT NULL,
    email VARCHAR(255) NOT NULL,
    password VARCHAR(255) NOT NULL,
    validated VARCHAR(255) NOT NULL,
    registrationdate DATE NOT NULL
);

CREATE TABLE event (
    event_id BIGSERIAL PRIMARY KEY,
    eventname VARCHAR(255) NOT NULL,
    startdate timestamptz NOT NULL,
    enddate timestamptz NOT NULL,
    createdby BIGINT,
    isprivate BOOLEAN NOT NULL,
    locationpausetime INT NOT NULL,
    basepausetime INT NOT NULL,
    locweight INT NOT NULL,
    typeweight INT NOT NULL,
    compweight INT NOT NULL,
    groupweight INT NOT NULL,
    CONSTRAINT event_createdby_foreign
        FOREIGN KEY (createdby) REFERENCES users(user_id)
);

CREATE TABLE eventtype (
    eventtype_id BIGSERIAL PRIMARY KEY,
    event_id BIGINT NOT NULL,
    typename VARCHAR(255) NOT NULL,
    timerange TIME NOT NULL,
    CONSTRAINT eventtype_event_id_foreign
        FOREIGN KEY (event_id) REFERENCES event(event_id) ON DELETE CASCADE
);

CREATE TABLE groups (
    group_id BIGSERIAL PRIMARY KEY,
    event_id BIGINT NOT NULL,
    groupname VARCHAR(255) NOT NULL,
    CONSTRAINT group_event_id_foreign
        FOREIGN KEY (event_id) REFERENCES event(event_id)
);

CREATE TABLE participant (
    participant_id BIGSERIAL PRIMARY KEY,
    competitornumber INT NOT NULL,
    participantname VARCHAR(255) NOT NULL,
    event_id BIGINT NOT NULL,
    group_id BIGINT,
    CONSTRAINT participant_event_id_foreign
        FOREIGN KEY (event_id) REFERENCES event(event_id),
    CONSTRAINT participant_group_id_foreign
        FOREIGN KEY (group_id) REFERENCES groups(group_id) ON DELETE CASCADE
);

CREATE TABLE location (
    location_id BIGSERIAL PRIMARY KEY,
    event_id BIGINT NOT NULL,
    locationname VARCHAR(255) NOT NULL,
    slots INT NOT NULL,
    CONSTRAINT location_event_id_foreign
        FOREIGN KEY (event_id) REFERENCES event(event_id)
);

CREATE TABLE schedule (
    schedule_id BIGSERIAL PRIMARY KEY,
    eventtype_id BIGINT NOT NULL,
    participant_id BIGINT NOT NULL,
    location_id BIGINT NOT NULL,
    starttime timestamptz NOT NULL,
    endtime timestamptz NOT NULL,
    slot INT NOT NULL,
    CONSTRAINT schedule_eventtype_id_foreign
        FOREIGN KEY (eventtype_id) REFERENCES eventtype(eventtype_id) ON DELETE CASCADE,
    CONSTRAINT schedule_participant_id_foreign
        FOREIGN KEY (participant_id) REFERENCES participant(participant_id) ON DELETE CASCADE,
    CONSTRAINT schedule_location_id_foreign
        FOREIGN KEY (location_id) REFERENCES location(location_id) ON DELETE CASCADE
);

CREATE TABLE registrations (
    registration_id BIGSERIAL PRIMARY KEY,
    event_id BIGINT NOT NULL,
    participant_id BIGINT NOT NULL,
    eventtype_id BIGINT NOT NULL,
    CONSTRAINT registrations_event_id_foreign
        FOREIGN KEY (event_id) REFERENCES event(event_id),
    CONSTRAINT registrations_participant_id_foreign
        FOREIGN KEY (participant_id) REFERENCES participant(participant_id) ON DELETE CASCADE,
    CONSTRAINT registrations_eventtype_id_foreign
        FOREIGN KEY (eventtype_id) REFERENCES eventtype(eventtype_id) ON DELETE CASCADE
);

CREATE TABLE pausetable (
    pause_id BIGSERIAL PRIMARY KEY,
    event_id BIGINT NOT NULL,
    location_id1 BIGINT NOT NULL,
    location_id2 BIGINT NOT NULL,
    pause TIME NOT NULL,
    CONSTRAINT pausetable_event_id_foreign
        FOREIGN KEY (event_id) REFERENCES event(event_id) ON DELETE CASCADE,
    CONSTRAINT pausetable_location_id1_foreign
        FOREIGN KEY (location_id1) REFERENCES location(location_id) ON DELETE CASCADE,
    CONSTRAINT pausetable_location_id2_foreign
        FOREIGN KEY (location_id2) REFERENCES location(location_id)
);

CREATE TABLE log (
    log_id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL,
    logtext VARCHAR(255) NOT NULL,
    logdate timestamptz NOT NULL,
    CONSTRAINT log_user_id_foreign
        FOREIGN KEY (user_id) REFERENCES users(user_id)
);

CREATE TABLE locationtable (
    locationtable_id BIGSERIAL PRIMARY KEY,
    event_id BIGINT NOT NULL,
    eventtype_id BIGINT NOT NULL,
    location_id BIGINT NOT NULL,
    CONSTRAINT locationtable_event_id_foreign
        FOREIGN KEY (event_id) REFERENCES event(event_id),
    CONSTRAINT locationtable_eventtype_id_foreign
        FOREIGN KEY (eventtype_id) REFERENCES eventtype(eventtype_id) ON DELETE CASCADE,
    CONSTRAINT locationtable_location_id_foreign
        FOREIGN KEY (location_id) REFERENCES location(location_id) ON DELETE CASCADE
);

CREATE TABLE eventconstraint (
    constraint_id BIGSERIAL PRIMARY KEY,
    event_id BIGINT NOT NULL,
    object_id BIGINT NOT NULL,
    constrainttype CHAR(1) NOT NULL,
    starttime timestamptz NOT NULL,
    endtime timestamptz NOT NULL,
    CONSTRAINT constraint_event_id_foreign
        FOREIGN KEY (event_id) REFERENCES event(event_id)
);

/*
modelBuilder.HasDefaultSchema("public");
*/

/*
dotnet ef dbcontext scaffold "Host=localhost;Database=scheduleLogic;Username=app;Password=app" Npgsql.EntityFrameworkCore.PostgreSQL -o Models -f
*/