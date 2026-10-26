namespace ScheduleLogic.Server.Class
{
    public static class AdminModels
    {
        public class AdminStatsDTO
        {
            public int UserCount { get; set; }
            public bool DatabaseAvailable { get; set; }
            public bool SolverAvailable { get; set; }
        }

        public class AdminUserDTO
        {
            public long UserId { get; set; }
            public string Username { get; set; } = "";
            public string Email { get; set; } = "";
            public DateOnly RegistrationDate { get; set; }
            public bool EmailConfirmed { get; set; }
        }
    }
}