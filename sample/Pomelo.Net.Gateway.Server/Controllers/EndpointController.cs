using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.Server.Models;

namespace Pomelo.Net.Gateway.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EndpointController : ControllerBase
    {
        public async ValueTask<ApiResult<Endpoint>> Get(
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        { 
            
        }
    }
}
