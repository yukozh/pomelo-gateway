using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pomelo.Net.Gateway.Server.Models;

namespace Pomelo.Net.Gateway.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        public async ValueTask<ApiResult<List<User>>> Get(
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            return ApiResult(await db.Users
                .ToListAsync(cancellationToken));
        }

        [HttpGet("{id}")]
        public async ValueTask<ApiResult<User>> GetSingle(
            string id, 
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            var user = await db.Users
                .Include(x => x.AllowedEndpoints)
                .SingleOrDefaultAsync(cancellationToken);
            if (user == null)
            {
                return ApiResult<User>(404, "The specified user is not found");
            }
            return ApiResult(user);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        [HttpPatch("{id}")]
        public async ValueTask<ApiResult> Patch(
            string id,
            [FromServices] ServerContext db,
            [FromBody] User model,
            CancellationToken cancellationToken = default)
        {
            var user = await db.Users.SingleOrDefaultAsync(cancellationToken);
            if (user == null)
            {
                db.Users.Add(model);
                await db.SaveChangesAsync();
            }
            else
            {
                user.AllowCreateOnDemandEndpoint = model.AllowCreateOnDemandEndpoint;
                user.Role = model.Role;
                if (!string.IsNullOrEmpty(model.Password))
                {
                    user.Password = model.Password;
                }
                await db.SaveChangesAsync(cancellationToken);
            }
            return ApiResult(200, "Succeeded");
        }

        [HttpDelete("{id}")]
        public async ValueTask<ApiResult> Delete(
            string id,
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            var user = await db.Users.SingleOrDefaultAsync(cancellationToken);

            if (user == null)
            {
                return ApiResult(404, "The specified user is not found");
            }

            if (user.Username == User.Identity.Name)
            {
                return ApiResult(400, "You cannot delete yourself");
            }

            db.Users.Remove(user);
            await db.SaveChangesAsync(cancellationToken);
            return ApiResult(200, "Succeeded");
        }
    }
}
