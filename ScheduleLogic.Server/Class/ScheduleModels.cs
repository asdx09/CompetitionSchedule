namespace ScheduleLogic.Server.Class
{
    public class ScheduleModels
    {
        #region Backend to SOLVER
        public class LocationModel
        {
            public long Id { get; set; }
            public string Name { get; set; } = null!;
            public int Capacity { get; set; }
        }
        public class EventModel
        {
            public long Id { get; set; }
            public string Name { get; set; } = null!;
            public int Duration { get; set; }
            public List<long> PossibleLocations { get; set; } = null!;
        }
        public class CompetitorModel
        {
            public long Id { get; set; }
            public string Name { get; set; } = null!;
            public long GroupId { get; set; }
        }
        public class EntryModel
        {
            public long Id { get; set; }
            public long CompetitorId { get; set; }
            public long EventId { get; set; }
        }
        public class GroupModel
        {
            public long Id { get; set; }
            public string Name { get; set; } = null!;
            public long EventId { get; set; }
        }
        public class ConstraintModel
        {
            public long Id { get; set; }
            public long ObjectId { get; set; }
            public string ConstraintType { get; set; } = null!; //'L' location, 'C' competitor, 'E' event, 'G' Group
            public int StartTime { get; set; }
            public int EndTime { get; set; }
        }
        public partial class PauseTableModel
        {
            public long Id { get; set; }
            public long LocationID1 { get; set; }
            public long LocationID2 { get; set; }
            public int Pause { get; set; }
        }
        public class ScheduleRequestForSolver
        {
            public string ReturnURL { get; set; } = "https://localhost:7098/api/Schedule/answer";
            public int EventId { get; set; }
            public List<LocationModel> Locations { get; set; } = null!;
            public List<EventModel> Events { get; set; } = null!;
            public List<CompetitorModel> Competitors { get; set; } = null!;
            public List<EntryModel> Entries { get; set; } = null!;
            public List<PauseTableModel> Travel { get; set; } = null!;
            public List<ConstraintModel> Constraints { get; set; } = null!;
            public int DayLength { get; set; }
            public int MaxDays { get; set; }
            public int BreakTimeLoc { get; set; }
            public int BasePauseTime { get; set; }
            public int LocWeight { get; set; }
            public int GroupWeight { get; set; }
            public int TypeWeight { get; set; }
            public int CompWeight { get; set; }
        }
        #endregion

        #region SOLVER to backend
        public class SolverResponse
        {
            public int EventId { get; set; }
            public string Status { get; set; } = null!;
            public List<ScheduleModel> Schedule { get; set; } = null!;
        }
        public class ScheduleModel
        {
            public int ParticipantId { get; set; }
            public int EventTypeId { get; set; }
            public int LocationId { get; set; }
            public int Start { get; set; }
            public int End { get; set; }
            public int Slot { get; set; }
        }
        public class SolverStatusResponse
        {
            public bool Running { get; set; }
        }
        public class StopSolverResponse
        {
            public string Status { get; set; } = null!;
        }
        #endregion

        #region EXPORT
        public class ScheduleTimeZoneEXPORT
        {
            public string EventType { get; set; } = null!;
            public string Participant { get; set; } = null!;
            public string GroupName { get; set; } = null!;
            public string Location { get; set; } = null!;
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public int Slot { get; set; }
        }
        public class ScheduleDataForEXPORT
        {
            public List<ScheduleTimeZoneEXPORT> TimeZones { get; set; } = null!;
            public string EventName { get; set; } = null!;
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }
        #endregion
    }
}
