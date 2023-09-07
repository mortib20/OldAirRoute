using AirRoute.ADSB;
using Microsoft.AspNetCore.Mvc;

namespace Main.Data
{
    public class ADSBController : Controller
    {
        private readonly RouterManager _routerManager;

        public ADSBController(RouterManager routerManager)
        {
            _routerManager = routerManager;
        }

        public IActionResult Index()
        {
            _routerManager.AddOutput("feed.adsbexchange.com", 30004);
            return View();
        }
    }
}
