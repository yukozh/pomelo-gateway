using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Association.Models;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Server.Models;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Server.Controllers
{
    [Authorize]
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

        public IActionResult Tunnel(string id)
        {
            ViewBag.Identifier = string.IsNullOrWhiteSpace(id) ? null : id;
            return View();
        }

        public async ValueTask<IActionResult> Endpoint(
            [FromServices] IServiceProvider services, 
            [FromServices] EndpointContext db)
        {
            ViewBag.StreamTunnels = services.GetServices<IStreamTunnel>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToDictionary(x => x.Id);
            ViewBag.StreamRouters = services.GetServices<IStreamRouter>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToDictionary(x => x.Id);
            ViewBag.PacketTunnels = services.GetServices<IPacketTunnel>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToDictionary(x => x.Id);
            ViewBag.PacketRouters = services.GetServices<IPacketRouter>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToDictionary(x => x.Id);

            return View(await db.Endpoints
                .Include(x => x.Users)
                .ToListAsync());
        }

        public async ValueTask<IActionResult> User(
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            return View(await db.Users.OrderByDescending(x => x.Role).ToListAsync());
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
