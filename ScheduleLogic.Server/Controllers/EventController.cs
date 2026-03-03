using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Services;
using ScheduleLogic.Server.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ScheduleLogic.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IEventService _eventService;

        public EventController(IConfiguration configuration , IEventService eventService)
        {
            _configuration = configuration;
            _eventService = eventService;
        }

        [Authorize]
        [HttpPost("new-event")]
        public async Task<IActionResult> NewEvent()
        {
            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                await _eventService.NewEvent(username);
                return Ok();
            }
            return Unauthorized();
        }

        [Authorize]
        [HttpDelete("delete-event")]
        public async Task<IActionResult> DeleteEvent([FromQuery] string id)
        {
            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                await _eventService.DeleteEvent(id, username);
                return Ok();
            }
            return Forbid();
        }

        [Authorize]
        [HttpGet("get-events")]
        public async Task<IActionResult> GetEvents()
        {
            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                var result = await _eventService.GetEvents(username);
                return Ok(result);
            }
            return Forbid();
        }

        [Authorize]
        [HttpGet("get-event")]
        public async Task<IActionResult> GetEvent([FromQuery] string id)
        {
            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                var result = await _eventService.GetEvent(id, username);
                if (result != null) return Ok(result);
                else return BadRequest();
            }
            return Forbid();
        }

        [Authorize]
        [HttpPost("save-event")]
        public async Task<IActionResult> SaveEvent([FromBody] EventModels.DataDTO Data)
        {
            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                bool result = await _eventService.SaveEvent(Data,username);
                if (result) return Ok();
                else return BadRequest();
            }
            return Forbid();
        }

        [Authorize]
        [HttpPost("new-wizard")]
        public async Task<IActionResult> NewWizardEvent([FromBody] EventModels.DataDTO Data)
        {
            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                var result = await _eventService.NewWizardEvent(Data, username);
                if (result) Ok();
                else return BadRequest();
            }
            return Forbid();
        }
    }
}
