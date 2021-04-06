using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Server.Models;

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
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            return View(await db.PublicRules.ToListAsync(cancellationToken));
        }
    }
}
