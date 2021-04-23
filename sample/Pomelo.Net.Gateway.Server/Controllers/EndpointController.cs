using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pomelo.Net.Gateway.Server.Models;

namespace Pomelo.Net.Gateway.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EndpointController : ControllerBase
    {
        public async ValueTask<ApiResult<List<Endpoint>>> Get(
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            return ApiResult(await db.Endpoints
                .ToListAsync(cancellationToken));
        }

        [HttpGet("{id:Guid}")]
        public async ValueTask<ApiResult<Endpoint>> GetSingle(
            [FromServices] ServerContext db,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var endpoint = await db.Endpoints
                .Include(x => x.EndpointUsers)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (endpoint == null)
            {
                return ApiResult<Endpoint>(404, "The specified endpoint is not found");
            }

            return ApiResult(endpoint);
        }

        [HttpPut("{id:Guid}")]
        [HttpPost("{id:Guid}")]
        [HttpPatch("{id:Guid}")]
        public async ValueTask<ApiResult> Patch(
            [FromServices] ServerContext db,
            Guid id,
            Endpoint model,
            CancellationToken cancellationToken = default)
        {
            var endpoint = await db.Endpoints
                .Include(x => x.EndpointUsers)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (endpoint == null)
            {
                return ApiResult<Endpoint>(404, "The specified endpoint is not found");
            }

            endpoint.TunnelId = model.TunnelId;
            endpoint.RouterId = model.RouterId;
            endpoint.Name = model.Name;
            await db.SaveChangesAsync(cancellationToken);
            return ApiResult(200, "Succeeded");
        }

        [HttpPut]
        [HttpPost]
        [HttpPatch]
        public async ValueTask<ApiResult<Endpoint>> Post(
            [FromServices] ServerContext db,
            Endpoint model,
            CancellationToken cancellationToken = default)
        {
            if (await db.Endpoints.AnyAsync(x => x.Address == model.Address
                && x.Port == model.Port
                && x.Protocol == model.Protocol, cancellationToken))
            {
                return ApiResult<Endpoint>(400, "The endpoint is already existed");
            }

            db.Endpoints.Add(model);
            await db.SaveChangesAsync(cancellationToken);
            return ApiResult(model);
        }

        [HttpDelete("{id:Guid}")]
        public async ValueTask<ApiResult> Delete(
            [FromServices] ServerContext db,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var endpoint = await db.Endpoints
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (endpoint == null)
            {
                return ApiResult<Endpoint>(404, "The specified endpoint is not found");
            }

            db.Endpoints.Remove(endpoint);
            await db.SaveChangesAsync(cancellationToken);
            return ApiResult(200, "Succeeded");
        }

        [HttpPut("{id:Guid}/users/{user}")]
        [HttpPost("{id:Guid}/users/{user}")]
        [HttpPatch("{id:Guid}/users/{user}")]
        public async ValueTask<ApiResult> PutEndpointUser(
            [FromServices] ServerContext db,
            Guid id,
            string user,
            CancellationToken cancellationToken = default)
        {
            var endpoint = await db.Endpoints
                .Include(x => x.EndpointUsers)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (endpoint == null)
            {
                return ApiResult<Endpoint>(404, "The specified endpoint is not found");
            }

            if (endpoint.EndpointUsers.Any(x => x.UserId == user))
            {
                return ApiResult(400, $"The user {user} is already authorized to use this endpoint");
            }

            endpoint.EndpointUsers.Add(new EndpointUser { EndpointId = id, UserId = user });
            await db.SaveChangesAsync(cancellationToken);

            return ApiResult(200, "Succeeded");
        }

        public async ValueTask<ApiResult> DeleteEndpointUser(
            [FromServices] ServerContext db,
            Guid id,
            string user,
            CancellationToken cancellationToken = default)
        {
            var endpoint = await db.Endpoints
              .Include(x => x.EndpointUsers)
              .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (endpoint == null)
            {
                return ApiResult<Endpoint>(404, "The specified endpoint is not found");
            }

            if (endpoint.EndpointUsers.All(x => x.UserId != user))
            {
                return ApiResult(400, $"The user {user} is not authorized to use this endpoint");
            }

            var endpointUser = endpoint.EndpointUsers.Single(x => x.UserId == user);
            endpoint.EndpointUsers.Remove(endpointUser);
            await db.SaveChangesAsync(cancellationToken);
            return ApiResult(200, "Succeeded");
        }
    }
}
