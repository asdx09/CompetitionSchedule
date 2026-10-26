using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleLogic.Server.Services.Interfaces;
using System.Net;
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

        public ScheduleController(IScheduleService scheduleService, ILogger<ScheduleController> logger)
        {
            _scheduleService = scheduleService;
            _logger = logger;
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

        // A solver hívja vissza, nincs bejelentkezett felhasználó, ezért
        // [Authorize] helyett a hívó IP-jét ellenőrizzük: csak ugyanarról
        // a gépről (localhost) fogadjuk el. Ha a solver külön szerveren/
        // konténerben fut, ez a kliens gép IP-je lesz, és ezt a listát
        // (vagy egy reverse-proxy szabályt) kell majd bővíteni.
        [HttpPost("answer")]
        public async Task<IActionResult> SolverAnswer([FromBody] SolverResponse data)
        {
            var remoteIp = HttpContext.Connection.RemoteIpAddress;
            bool isLocal = remoteIp != null && IPAddress.IsLoopback(remoteIp);

            if (!isLocal)
            {
                _logger.LogWarning("Rejected /answer call from non-local address {Ip}", remoteIp);
                return Forbid();
            }

            await _scheduleService.ReceiveSolverAnswer(data);
            return Ok();
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