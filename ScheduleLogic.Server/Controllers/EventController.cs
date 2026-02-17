using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Models;
using ScheduleLogic.Server.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ScheduleLogic.Server.Controllers
{
    public class EventsData
    {
        public string eventName { get; set; }
        public long event_ID { get; set; }
    }


    [ApiController]
    [Route("api/[controller]")]
    public class EventController : ControllerBase
    {
        private readonly DatabaseService _dbService;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;

        public EventController(IMapper mapper, DatabaseService dbService, IConfiguration configuration)
        {
            _mapper = mapper;
            _dbService = dbService;
            _configuration = configuration;
        }

        [Authorize]
        [HttpPost("new-event")]
        public IActionResult NewEvent()
        {
            _dbService.NewEvent(User.Identity.Name);
            return Ok();
        }

        [Authorize]
        [HttpDelete("delete-event")]
        public IActionResult DeleteEvent([FromQuery] string id)
        {
            if (!_dbService.CheckUser(User.Identity.Name, id)) return Forbid();
            _dbService.DeleteEvent(Convert.ToInt32(id));
            return Ok();
        }

        [Authorize]
        [HttpGet("get-events")]
        public IActionResult GetEvents()
        {
            return Ok(_dbService.GetEvents(User.Identity.Name));
        }

        [Authorize]
        [HttpGet("get-event")]
        public IActionResult GetEvent([FromQuery] string id)
        {
            if (!_dbService.CheckUser(User.Identity.Name, id)) return Forbid();
            return Ok(_dbService.GetEvent(id, User.Identity.Name));
        }

        [Authorize]
        [HttpPost("save-event")]
        public IActionResult SaveEvent([FromBody] dataDTO Data)
        {
            if (!_dbService.CheckUser(User.Identity.Name, Data.eventData.EventId.ToString())) return Forbid();
            bool res = _dbService.SaveEvent(Data);
            if (res) return Ok();
            else return BadRequest();
        }

        [Authorize]
        [HttpPost("new-wizard")]
        public IActionResult NewWizardEvent([FromBody] dataDTO Data)
        {
            _dbService.NewWizardEvent(Data, User.Identity.Name);
            //try { _dbService.NewWizardEvent(Data, User.Identity.Name); } catch { return BadRequest(); }
            return Ok();
        }
    }
}
