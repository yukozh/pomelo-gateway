using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Association.Models;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Tunnel;
using Pomelo.WebSlotGateway.Models;

namespace Pomelo.WebSlotGateway.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index([FromServices] IServiceProvider services)
        {
            ViewBag.StreamTunnels = services.GetServices<IStreamTunnel>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList();
            ViewBag.StreamRouters = services.GetServices<IStreamRouter>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList();
            ViewBag.PacketTunnels = services.GetServices<IPacketTunnel>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList();
            ViewBag.PacketRouters = services.GetServices<IPacketRouter>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList();

            return View();
        }

        public IActionResult Slot()
            => View();

        public IActionResult Config()
            => View();

        public IActionResult Tunnel()
            => View();
        public IActionResult ARR()
            => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
