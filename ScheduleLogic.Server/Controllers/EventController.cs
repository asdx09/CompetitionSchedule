using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Services.Interfaces;
using System.Security.Claims;

namespace ScheduleLogic.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EventController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IEventService _eventService;

        public EventController(IConfiguration configuration, IEventService eventService)
        {
            _configuration = configuration;
            _eventService = eventService;
        }

        // A UserId a bejelentkezéskor kiállított cookie-ban lévő claimből jön,
        // nincs hozzá adatbázis-keresés. [Authorize] miatt a claim mindig jelen
        // van, ha idáig eljutott a kérés, ezért ez itt sosem "Unauthorized",
        // hanem programozási hiba jele lenne — ezért dob, nem Forbid()-ot ad.
        private long UserId =>
            long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("new-event")]
        public async Task<IActionResult> NewEvent()
        {
            await _eventService.NewEvent(UserId);
            return Ok();
        }

        [HttpDelete("delete-event")]
        public async Task<IActionResult> DeleteEvent([FromQuery] long id)
        {
            var deleted = await _eventService.DeleteEvent(id, UserId);
            return deleted ? Ok() : NotFound();
        }

        [HttpGet("get-events")]
        public async Task<IActionResult> GetEvents()
        {
            var result = await _eventService.GetEvents(UserId);
            return Ok(result);
        }

        [HttpGet("get-event")]
        public async Task<IActionResult> GetEvent([FromQuery] long id)
        {
            var result = await _eventService.GetEvent(id, UserId);
            return result != null ? Ok(result) : NotFound();
        }

        [HttpPost("save-event")]
        public async Task<IActionResult> SaveEvent([FromBody] EventModels.DataDTO data)
        {
            var result = await _eventService.SaveEvent(data, UserId);
            return result ? Ok() : BadRequest();
        }

        [HttpPost("new-wizard")]
        public async Task<IActionResult> NewWizardEvent([FromBody] EventModels.DataDTO data)
        {
            var result = await _eventService.NewWizardEvent(data, UserId);
            return result ? Ok() : BadRequest();
        }
    }
}