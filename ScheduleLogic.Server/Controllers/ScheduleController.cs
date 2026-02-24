using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleLogic.Server.Services;
using System.Text.Json;


namespace ScheduleLogic.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScheduleController : ControllerBase
    {
        private readonly DatabaseService _dbService;
        private readonly IConfiguration _configuration;
        private readonly ScheduleService _scheduleService;

        public ScheduleController(DatabaseService dbService, IConfiguration configuration, ScheduleService scheduleService)
        {
            _dbService = dbService;
            _configuration = configuration;
            _scheduleService = scheduleService;
        }


        [Authorize]
        [HttpPost("{id}")]
        public async Task<IActionResult> GenerateSchedule(int id)
        {
            var result = await _scheduleService.GenerateSchedule(id);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSchedule(string id)
        {
            var result = await _scheduleService.GetScheduleData(id);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("isRunning")]
        public async Task<IActionResult> CheckSolver(string id)
        {
            try
            {
                var result = await _scheduleService.CheckSolver(id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return NotFound(ex);
            }
        }

        [Authorize]
        [HttpPost("stop")]
        public async Task<IActionResult> StopSolver(string id)
        {
            var result = await _scheduleService.StopSolver(id);
            return Ok();
        }

        [HttpPost("answer")]
        public async Task<IActionResult> SolverAnswer(SolverResponse data)
        {
            if (data.status == "FEASIBLE" || data.status == "PARTIAL_SOLUTION" || data.status == "OPTIMAL" || data.status == "4" || data.status == "1")
            {
                _dbService.NewSchedule(data.schedule, data.event_id);
            }
            else
            {
                Console.WriteLine("No solution found! " + data.status);
            }
            return Ok();
        }

        [Authorize]
        [HttpGet("export")]
        public async Task<IActionResult> GetExport(string id)
        {
            try
            {
                var content = await _scheduleService.GetScheduleFile(id);
                return File(
                    content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"schedule.xlsx"
                );
            }
            catch (Exception ex)
            {
                return NotFound(ex);
            }
        }
        
    }
}

