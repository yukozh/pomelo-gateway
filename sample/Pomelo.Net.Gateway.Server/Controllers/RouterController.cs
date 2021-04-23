using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Association.Models;
using Pomelo.Net.Gateway.Router;

namespace Pomelo.Net.Gateway.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RouterController : ControllerBase
    {
        [HttpGet("stream/providers")]
        public ApiResult<dynamic> GetStreamRouters(
            [FromServices] IServiceProvider services)
        {
            var tunnelProviders = services.GetServices<IStreamRouter>();
            return ApiResult<dynamic>(tunnelProviders.Select(x => new
            {
                Id = x.Id,
                Name = x.Name
            }).ToList());
        }

        [HttpGet("packet/providers")]
        public ApiResult<dynamic> GetPacketRouters(
            [FromServices] IServiceProvider services)
        {
            var tunnelProviders = services.GetServices<IPacketRouter>();
            return ApiResult<dynamic>(tunnelProviders.Select(x => new
            {
                Id = x.Id,
                Name = x.Name
            }).ToList());
        }
    }
}
