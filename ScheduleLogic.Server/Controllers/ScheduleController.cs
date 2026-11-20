using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleLogic.Server.Services.Interfaces;
using ScheduleLogic.Server.Services.Security;
using System.Security.Claims;
using static ScheduleLogic.Server.Class.ScheduleModels;

namespace ScheduleLogic.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScheduleController : ControllerBase
    {
        private readonly IScheduleService _scheduleService;
        private readonly ILogger<ScheduleController> _logger;
        private readonly SolverCallbackSigner _signer;

        public ScheduleController(IScheduleService scheduleService, ILogger<ScheduleController> logger, SolverCallbackSigner signer)
        {
            _scheduleService = scheduleService;
            _logger = logger;
            _signer = signer;
        }

        private long UserId =>
            long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [Authorize]
        [HttpPost("{id:long}")]
        public async Task<IActionResult> GenerateSchedule(long id)
        {
            var result = await _scheduleService.GenerateSchedule(id, UserId);
            return result != null ? Ok(result) : NotFound();
        }

        [Authorize]
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetSchedule(long id)
        {
            var result = await _scheduleService.GetScheduleData(id, UserId);
            return result != null ? Ok(result) : NotFound();
        }

        [Authorize]
        [HttpGet("isRunning")]
        public async Task<IActionResult> CheckSolver([FromQuery] long id)
        {
            var result = await _scheduleService.CheckSolver(id, UserId);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [Authorize]
        [HttpPost("stop")]
        public async Task<IActionResult> StopSolver([FromQuery] long id)
        {
            var result = await _scheduleService.StopSolver(id, UserId);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("answer")]
        public async Task<IActionResult> SolverAnswer([FromBody] SolverResponse data, [FromQuery] string? key)
        {
            if (!_signer.Verify(data.EventId, key))
            {
                _logger.LogWarning("Rejected /answer call with invalid key for event {EventId} from {Ip}",
                    data.EventId, HttpContext.Connection.RemoteIpAddress);
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var accepted = await _scheduleService.ReceiveSolverAnswer(data);
            return accepted ? Ok() : Problem(statusCode: StatusCodes.Status400BadRequest);
        }

        [Authorize]
        [HttpGet("export")]
        public async Task<IActionResult> GetExport([FromQuery] long id)
        {
            var content = await _scheduleService.GetScheduleFile(id, UserId);
            if (content == null) return NotFound();

            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "schedule.xlsx"
            );
        }
    }
}