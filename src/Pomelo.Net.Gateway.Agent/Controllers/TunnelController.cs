using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Association.Models;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Agent.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TunnelController : ControllerBase
    {
        [HttpGet("stream")]
        public IEnumerable<Interface> GetStreamTunnels([FromServices] IServiceProvider services)
        {
            return services.GetServices<IStreamTunnel>()
                .Select(x => new Interface 
                {
                    Id = x.Id,
                    Name = x.Name
                });
        }

        [HttpGet("packet")]
        public IEnumerable<Interface> GetPacketTunnels([FromServices] IServiceProvider services)
        {
            return services.GetServices<IPacketTunnel>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                });
        }
    }
}
