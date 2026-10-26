using static ScheduleLogic.Server.Class.EventModels;
using static ScheduleLogic.Server.Class.ScheduleModels;

namespace ScheduleLogic.Server.Services.Interfaces
{
    public interface IScheduleRepository
    {
        Task<ScheduleRequestForSolver> GetScheduleInfoAsync(long eventId);
        Task<bool> SaveSolverResultAsync(List<ScheduleModel> schedule, long eventId);
        Task<DataDTO> GetScheduleDataAsync(long eventId);
        Task<ScheduleDataForEXPORT?> GetExportDataAsync(long eventId);
    }
}
