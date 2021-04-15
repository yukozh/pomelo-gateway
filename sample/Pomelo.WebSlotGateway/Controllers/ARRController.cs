using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Router;
using Pomelo.WebSlotGateway.Utils;

namespace Pomelo.WebSlotGateway.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ARRController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<ARRContext> GetStreamTunnels()
            => (HttpContext.RequestServices
                    .GetServices<IStreamRouter>()
                    .Single(x => x is ARRAffinityRouter) as ARRAffinityRouter)
                .Contexts;
    }
}
