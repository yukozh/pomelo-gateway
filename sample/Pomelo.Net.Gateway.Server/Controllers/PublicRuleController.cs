using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Pomelo.Net.Gateway.Association.Models;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.EndpointManager;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Server.Models;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Server.Controllers
{
    public class PublicRuleController : Controller
    {
        private readonly ILogger<PublicRuleController> _logger;

        public PublicRuleController(ILogger<PublicRuleController> logger)
        {
            _logger = logger;
        }

        public async ValueTask<IActionResult> Index(
            [FromServices] IServiceProvider services,
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
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

            return View(await db.PublicRules.ToListAsync(cancellationToken));
        }

        [HttpGet]
        public IActionResult Create([FromServices] IServiceProvider services)
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

        [HttpPost]
        public async ValueTask<IActionResult> Create(
            [FromServices] ServerContext db,
            [FromServices] TcpEndPointManager tcpEndpointManager,
            [FromServices] UdpEndPointManager udpEndpointManager,
            PublicRule model,
            CancellationToken cancellationToken = default)
        {
            var rules = GetStaticRules();
            if (rules.Any(x => x.Id == model.Id))
            {
                return Content($"The ID {model.Id} is conflicted");
            }

            IPEndPoint serverEndpoint;
            try
            {
                serverEndpoint = IPEndPoint.Parse(model.ServerEndpoint);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.ToString());
                return Content("Endpoint is invalid");
            }

            if (model.Protocol == Protocol.TCP)
            {
                rules.Add(new StaticRule
                {
                    Id = Guid.NewGuid().ToString(),
                    DestinationEndpoint = IPEndPoint.Parse(model.DestinationEndpoint),
                    RouterId = model.RouterId,
                    ListenerEndpoint = serverEndpoint,
                    Protocol = Protocol.TCP,
                    TunnelId = model.TunnelId,
                    UnwrapSsl = false
                });
            }
            else
            {
                rules.Add(new StaticRule
                {
                    Id = Guid.NewGuid().ToString(),
                    DestinationEndpoint = IPEndPoint.Parse(model.DestinationEndpoint),
                    RouterId = model.RouterId,
                    ListenerEndpoint = serverEndpoint,
                    Protocol = Protocol.UDP,
                    TunnelId = model.TunnelId,
                    UnwrapSsl = false
                });
            }

            SetStaticRules(rules);

            await tcpEndpointManager.EnsureStaticRulesEndPointsCreatedAsync();
            await udpEndpointManager.EnsureStaticRulesEndPointsCreatedAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async ValueTask<IActionResult> Edit(
            string id,
            [FromServices] IServiceProvider services,
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
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

            var rule = await db.PublicRules.SingleAsync(x => x.Id == id, cancellationToken);
            return View(rule);
        }

        [HttpPost]
        public async ValueTask<IActionResult> Edit(
            PublicRule model,
            [FromServices] IServiceProvider services,
            [FromServices] TcpEndPointManager tcpEndpointManager,
            [FromServices] UdpEndPointManager udpEndpointManager,
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            var rules = GetStaticRules();
            rules = rules.Where(x => x.Id != model.Id).ToList();

            if (model.Protocol == Protocol.TCP)
            {
                rules.Add(new StaticRule
                {
                    Id = Guid.NewGuid().ToString(),
                    DestinationEndpoint = IPEndPoint.Parse(model.DestinationEndpoint),
                    RouterId = model.RouterId,
                    ListenerEndpoint = IPEndPoint.Parse(model.ServerEndpoint),
                    Protocol = Protocol.TCP,
                    TunnelId = model.TunnelId,
                    UnwrapSsl = false
                });
            }
            else
            {
                rules.Add(new StaticRule
                {
                    Id = Guid.NewGuid().ToString(),
                    DestinationEndpoint = IPEndPoint.Parse(model.DestinationEndpoint),
                    RouterId = model.RouterId,
                    ListenerEndpoint = IPEndPoint.Parse(model.ServerEndpoint),
                    Protocol = Protocol.UDP,
                    TunnelId = model.TunnelId,
                    UnwrapSsl = false
                });
            }

            ViewBag.Info = "Succeeded";

            await tcpEndpointManager.EnsureStaticRulesEndPointsCreatedAsync();
            await udpEndpointManager.EnsureStaticRulesEndPointsCreatedAsync();

            return await Edit(model.Id, services, db, cancellationToken);
        }

        [HttpPost]
        public async ValueTask<IActionResult> Delete(
            string id,
            [FromServices] TcpEndPointManager tcpEndpointManager,
            [FromServices] UdpEndPointManager udpEndpointManager,
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            var rules = GetStaticRules();
            rules = rules.Where(x => x.Id != id).ToList();
            SetStaticRules(rules);
            await tcpEndpointManager.EnsureStaticRulesEndPointsCreatedAsync();
            await udpEndpointManager.EnsureStaticRulesEndPointsCreatedAsync();
            return RedirectToAction(nameof(Index));
        }

        private static List<StaticRule> GetStaticRules()
        {
            var text = System.IO.File.ReadAllText("gateway-static-rules.json");
            return JsonConvert.DeserializeObject<List<StaticRule>>(text);
        }

        private static void SetStaticRules(IEnumerable<StaticRule> values)
        {
            System.IO.File.WriteAllText("gateway-static-rules.json", JsonConvert.SerializeObject(values));
        }
    }
}
