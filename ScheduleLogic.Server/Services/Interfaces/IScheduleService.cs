using static ScheduleLogic.Server.Class.EventModels;
using static ScheduleLogic.Server.Class.ScheduleModels;

namespace ScheduleLogic.Server.Services.Interfaces
{
    public interface IScheduleService
    {
        public Task<ScheduleRequestForSolver> GenerateSchedule(int id);

        public Task<DataDTO> GetScheduleData(string id);

        public Task<bool> CheckSolver(string id);

        public Task<bool> StopSolver(string id);

        public Task<byte[]> GetScheduleFile(string id);
    }
}
