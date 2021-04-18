using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Http
{
    public class TextHttpInterceptor : DefaultHttpInterceptor
    {
        public override bool CanIntercept(HttpHeader headers, HttpAction action)
            => action == HttpAction.Response && headers.ContentType.Contains("text/");

        public override async ValueTask BackwardResponseAsync(HttpTunnelContext context, CancellationToken cancellationToken = default)
        {
            using (var sw = new StreamWriter(context.ResponseDestinationStream, encoding: Encoding.ASCII, leaveOpen: false))
            using (var memory = MemoryPool<byte>.Shared.Rent(context.ResponseHeaders.ContentLength))
            {
                var count = 0;
                while (count < context.ResponseHeaders.ContentLength)
                {
                    var read = await context.ResponseSourceStream.ReadAsync(memory.Memory.Slice(count));
                    if (read == 0)
                    {
                        throw new InvalidDataException();
                    }
                    count += read;
                }
                var body = Encoding.ASCII.GetString(memory.Memory.Slice(0, context.ResponseHeaders.ContentLength).Span);
                body = body.Replace("Site", "Intercepted");
                context.ResponseHeaders.HeaderCollection["content-length"] = body.Length.ToString();
                await base.BackwardResponseHeaderAsync(context, cancellationToken);
                await sw.WriteAsync(body);
            }
        }
    }
}
