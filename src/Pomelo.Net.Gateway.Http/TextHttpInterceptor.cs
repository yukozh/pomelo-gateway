using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Http
{
    public class TextHttpInterceptor : DefaultHttpInterceptor
    {
        public override async ValueTask<bool> BackwardResponseAsync(HttpTunnelContext context, CancellationToken cancellationToken = default)
        {
            if (context.Response == null 
                || !context.Response.Headers.ContentType.Contains("text/"))
            {
                return false;
            }
            var body = await context.Response.ReadAsStringAsync();
            body = body.Replace("Site", "Intercepted");
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.Headers.AddOrUpdate("content-length", bytes.Length.ToString());
            await base.BackwardResponseHeaderAsync(context, cancellationToken);
            await context.Response.DestinationStream.WriteAsync(bytes, 0, bytes.Length);
            return true;
        }
    }
}
