using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.Http;

namespace Pomelo.WebSlotGateway.Utils
{
    public class GoogleInterceptor : DefaultHttpInterceptor
    {
        public override async ValueTask<bool> BackwardResponseAsync(HttpTunnelContext context, CancellationToken cancellationToken = default)
        {
            if (context.Response == null 
                || !context.Response.Headers.ContentType.Contains("text/html"))
            {
                return false;
            }
            var body = await context.Response.ReadAsStringAsync();
            body = body
                .Replace("\"https:\"==window.location.protocol", "(\"https:\"==window.location.protocol||\"http:\"==window.location.protocol)")
                .Replace("Google 搜索的运作方式 </a>", "Google 搜索的运作方式 </a><a class='pHiOh' href='https://github.com/pomelofoundation' target='_blank'>Pomelo Foundation</a>")
                .Replace("条款</a>", "条款</a><a class='JWaTvb Fx4vi' href='https://github.com/pomelofoundation' target='_blank'>Pomelo Foundation</a>");
            await context.Response.WriteTextAsync(
                body,
                "text/html",
                cancellationToken: cancellationToken);
            return true;
        }

        public override async ValueTask<bool> ForwardRequestAsync(HttpTunnelContext context, CancellationToken cancellationToken = default)
        {
            context.Request.Headers.AddOrUpdate("host", "www.google.com");
            return false;
        }
    }
}
