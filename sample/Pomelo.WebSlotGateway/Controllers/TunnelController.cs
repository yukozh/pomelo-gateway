using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Association.Models;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.WebSlotGateway.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TunnelController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<StreamTunnelContext> GetStreamTunnels([FromServices] StreamTunnelContextFactory streamTunnelContextFactory)
            => streamTunnelContextFactory.EnumerateContexts();
    }
}
