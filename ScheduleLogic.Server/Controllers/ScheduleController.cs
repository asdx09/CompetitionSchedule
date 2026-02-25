using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleLogic.Server.Services;
using System.Text.Json;
using static ScheduleLogic.Server.Class.ScheduleModels;


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
            if (data.Status == "FEASIBLE" || data.Status == "PARTIAL_SOLUTION" || data.Status == "OPTIMAL" || data.Status == "4" || data.Status == "1")
            {
                _dbService.NewSchedule(data.Schedule, data.EventId);
            }
            else
            {
                Console.WriteLine("No solution found! " + data.Status);
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

