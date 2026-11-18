using static ScheduleLogic.Server.Class.EventModels;
using static ScheduleLogic.Server.Class.ScheduleModels;

namespace ScheduleLogic.Server.Services.Interfaces
{
    public interface IScheduleService
    {
        Task<ScheduleRequestForSolver?> GenerateSchedule(long eventId, long userId);
        Task<DataDTO?> GetScheduleData(long eventId, long userId);
        Task<bool?> CheckSolver(long eventId, long userId);
        Task<bool?> StopSolver(long eventId, long userId);
        /// <returns>false, ha a beküldött beosztás nem az adott versenyhez tartozó objektumokra hivatkozik.</returns>
        Task<bool> ReceiveSolverAnswer(SolverResponse data);
        Task<byte[]?> GetScheduleFile(long eventId, long userId);
    }
}
