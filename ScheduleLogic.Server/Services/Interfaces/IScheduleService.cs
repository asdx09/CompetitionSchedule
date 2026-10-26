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
        Task ReceiveSolverAnswer(SolverResponse data);
        Task<byte[]?> GetScheduleFile(long eventId, long userId);
    }
}
