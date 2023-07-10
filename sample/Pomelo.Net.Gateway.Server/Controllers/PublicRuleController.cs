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
            if (await db.Users.AnyAsync(x => x.Username == model.Id))
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

            db.PublicRules.Add(model);
            await db.SaveChangesAsync();

            if (model.Protocol == EndpointCollection.Protocol.TCP)
            {
                await ruleProvider.c(
                    model.Id,
                    serverEndpoint,
                    model.DestinationEndpoint,
                    model.RouterId,
                    model.TunnelId,
                    false, // TODO: Support SSL
                    cancellationToken);
                tcpEndpointManager.GetOrCreateListenerForEndpoint(
                    serverEndpoint,
                    model.RouterId,
                    model.TunnelId,
                    model.Id,
                    EndpointCollection.EndpointUserType.Public);
            }
            else
            {
                await udpEndpointManager.InsertPreCreateEndpointRuleAsync(
                    model.Id,
                    serverEndpoint,
                    model.DestinationEndpoint,
                    model.RouterId,
                    model.TunnelId,
                    cancellationToken);
                udpEndpointManager.GetOrCreateListenerForEndpoint(
                    serverEndpoint,
                    model.RouterId,
                    model.TunnelId,
                    model.Id,
                    EndpointCollection.EndpointUserType.Public);
            }
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
            var rule = await db.PublicRules.SingleAsync(x => x.Id == model.Id, cancellationToken);
            var previousProtocol = rule.Protocol;
            rule.Protocol = model.Protocol;
            rule.RouterId = model.RouterId;
            rule.TunnelId = model.TunnelId;
            IPEndPoint serverEndpoint;
            try
            {
                serverEndpoint = IPEndPoint.Parse(model.ServerEndpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                ViewBag.Info = "Endpoint is invalid";
                return await Edit(model.Id, services, db, cancellationToken);
            }
            rule.DestinationEndpoint = model.DestinationEndpoint;
            rule.ServerEndpoint = serverEndpoint.ToString();
            await db.SaveChangesAsync(cancellationToken);

            // Remove old rule
            if (previousProtocol == EndpointCollection.Protocol.TCP)
            {
                await tcpEndpointManager.RemoveAllRulesFromAgentBridgeAsync(model.Id, cancellationToken);
                await tcpEndpointManager.RemovePreCreateEndpointRuleAsync(model.Id);
            }
            else
            {
                await udpEndpointManager.RemoveAllRulesFromUserIdentifierAsync(model.Id, cancellationToken);
                await udpEndpointManager.RemovePreCreateEndpointRuleAsync(model.Id);
            }

            // Create rule
            if (previousProtocol == EndpointCollection.Protocol.TCP)
            {
                await tcpEndpointManager.InsertPreCreateEndpointRuleAsync(
                    model.Id,
                    serverEndpoint,
                    model.DestinationEndpoint,
                    model.RouterId,
                    model.TunnelId,
                    false, // TODO: Support SSL
                    cancellationToken);
                tcpEndpointManager.GetOrCreateListenerForEndpoint(
                    serverEndpoint,
                    model.RouterId,
                    model.TunnelId,
                    model.Id,
                    EndpointCollection.EndpointUserType.Public);
            }
            else
            {
                await udpEndpointManager.InsertPreCreateEndpointRuleAsync(
                    model.Id,
                    serverEndpoint,
                    model.DestinationEndpoint,
                    model.RouterId,
                    model.TunnelId,
                    cancellationToken);
                udpEndpointManager.GetOrCreateListenerForEndpoint(
                    serverEndpoint,
                    model.RouterId,
                    model.TunnelId,
                    model.Id,
                    EndpointCollection.EndpointUserType.Public);
            }
            ViewBag.Info = "Succeeded";
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
            var rule = await db.PublicRules.SingleAsync(x => x.Id == id, cancellationToken);
            db.PublicRules.Remove(rule);
            await db.SaveChangesAsync(cancellationToken);
            if (rule.Protocol == EndpointCollection.Protocol.TCP)
            {
                await 
                await tcpEndpointManager.RemoveAllRulesFromAgentBridgeAsync(id, cancellationToken);
                await tcpEndpointManager.RemovePreCreateEndpointRuleAsync(id);
            }
            else
            {
                await udpEndpointManager.RemoveAllRulesFromUserIdentifierAsync(id, cancellationToken);
                await udpEndpointManager.RemovePreCreateEndpointRuleAsync(id);
            }
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
