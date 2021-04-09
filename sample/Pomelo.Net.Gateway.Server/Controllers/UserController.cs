using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Server.Models;

namespace Pomelo.Net.Gateway.Server.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;

        public UserController(ILogger<UserController> logger)
        {
            _logger = logger;
        }

        public async ValueTask<IActionResult> Index(
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
            => View(await db.Users
                .OrderByDescending(x => x.Role)
                .ToListAsync(cancellationToken));

        [HttpGet]
        public async ValueTask<IActionResult> Edit(
            string id,
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
            => View(nameof(Edit), await db.Users
                .Where(x => x.Username == id)
                .SingleAsync(cancellationToken));

        [HttpPost]
        public async ValueTask<IActionResult> Edit(
            User model,
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            var user = await db.Users
                .Where(x => x.Username == model.Username)
                .SingleAsync(cancellationToken);

            if (!string.IsNullOrEmpty(model.Password))
            {
                user.Password = model.Password;
            }
            if (User.Identity.Name == model.Username && model.Role != UserRole.Admin)
            {
                ViewBag.Info = "You cannot downgrade the role of yourself";
                return await Edit(user.Username, db, cancellationToken);
            }
            user.Role = model.Role;
            await db.SaveChangesAsync(cancellationToken);
            ViewBag.Info = "The user has been updated.";
            return await Edit(user.Username, db, cancellationToken);
        }

        public async ValueTask<IActionResult> Delete(
            string id,
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            if (id == User.Identity.Name)
            {
                ViewBag.Info = "You cannot delete yourself";
                return await Edit(id, db, cancellationToken);
            }

            var user = await db.Users
                .Where(x => x.Username == id)
                .SingleAsync(cancellationToken);
            db.Users.Remove(user);
            await db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        public async ValueTask<IActionResult> Create(
            User model, 
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            if (await db.Users.AnyAsync(
                x => x.Username == model.Username, 
                cancellationToken))
            {
                return Content("The username is already existed");
            }

            db.Users.Add(model);
            await db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
        }
    }
}
