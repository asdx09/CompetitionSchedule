using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ScheduleLogic.Server.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View(); 
        }
    }
}
