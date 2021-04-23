using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Association.Models;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TunnelController : ControllerBase
    {
        [HttpGet("stream/providers")]
        public ApiResult<dynamic> GetStreamTunnelProviders(
            [FromServices] IServiceProvider services)
        {
            var tunnelProviders = services.GetServices<IStreamTunnel>();
            return ApiResult<dynamic>(tunnelProviders.Select(x => new
            {
                Id = x.Id,
                Name = x.Name
            }).ToList());
        }

        [HttpGet("packet/providers")]
        public ApiResult<dynamic> GetPacketTunnelProviders(
            [FromServices] IServiceProvider services)
        {
            var tunnelProviders = services.GetServices<IPacketTunnel>();
            return ApiResult<dynamic>(tunnelProviders.Select(x => new
            {
                Id = x.Id,
                Name = x.Name
            }).ToList());
        }
    }
}
