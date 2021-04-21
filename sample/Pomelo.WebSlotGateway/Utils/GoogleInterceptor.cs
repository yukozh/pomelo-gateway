using System.IO;
using System.Linq;
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
            if (!(context.Request.Headers.Url == "/" || context.Request.Headers.Url.StartsWith("/search")))
            {
                return false;
            }

            using (var sr = new StreamReader(context.Response.Body, leaveOpen: true))
            using (var sw = new StreamWriter(context.Response.Body, leaveOpen: true))
            {
                while (true)
                {
                    if (cancellationToken.CanBeCanceled)
                    {
                        throw new TaskCanceledException();
                    }

                    if (!context.Response.Body.CanRead)
                    {
                        break;
                    }
                    var line = await sr.ReadLineAsync();
                    if (line == null)
                    {
                        break;
                    }

                    if (context.Request.Headers.Url == "/")
                    {
                        await sw.WriteLineAsync(line
                            .Replace("\"https:\"==window.location.protocol", "(\"https:\"==window.location.protocol||\"http:\"==window.location.protocol)")
                            .Replace("Google 搜索的运作方式 </a>", "Google 搜索的运作方式 </a><a class='pHiOh' href='https://github.com/pomelofoundation' target='_blank'>Pomelo Foundation</a>"));
                    }
                    else
                    {
                        await sw.WriteLineAsync(line
                            .Replace("\"https:\"==window.location.protocol", "(\"https:\"==window.location.protocol||\"http:\"==window.location.protocol)")
                            .Replace("条款</a>", "条款</a><a class='JWaTvb Fx4vi' href='https://github.com/pomelofoundation' target='_blank'>Pomelo Foundation</a>"));

                    }
                }
                await context.Response.Body.CompleteAsync();
            }
            return true;
        }

        public override async ValueTask<bool> ForwardRequestAsync(HttpTunnelContext context, CancellationToken cancellationToken = default)
            => false;
    }
}
