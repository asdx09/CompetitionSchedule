namespace ScheduleLogic.Server.Services
{
    public static class SolverStatus
    {
        public const string Queued = "QUEUED";
        public const string Running = "RUNNING";
        public const string Optimal = "OPTIMAL";
        public const string Feasible = "FEASIBLE";
        public const string Infeasible = "INFEASIBLE";
        public const string Unknown = "UNKNOWN";
        public const string ModelInvalid = "MODEL_INVALID";
        public const string Stopped = "STOPPED";
        public const string MemoryLimit = "MEMORY_LIMIT";
        public const string Error = "ERROR";

        private static readonly HashSet<string> Final = new()
        {
            Optimal, Feasible, Infeasible, Unknown, ModelInvalid, Stopped, MemoryLimit, Error
        };

        public static string FromSolver(string? status)
        {
            var s = (status ?? "").Trim().ToUpperInvariant();
            if (s == "PARTIAL_SOLUTION") return Running;
            return Final.Contains(s) ? s : Error;
        }
    }
}
