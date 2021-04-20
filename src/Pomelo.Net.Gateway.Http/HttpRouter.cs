using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.Router;

namespace Pomelo.Net.Gateway.Http
{
    public abstract class HttpRouterBase : IStreamRouter
    {
        public virtual Guid Id => Guid.Parse("24344d5a-085c-ead5-a94c-b541703573cf");

        public virtual string Name => "HTTP Router Base";

        public virtual int ExpectedBufferSize => 65536; // Max Limitation of HTTP Header

        public async ValueTask<RouteResult> DetermineIdentifierAsync(
            Stream stream,
            Memory<byte> buffer,
            IPEndPoint from,
            CancellationToken cancellationToken = default)
        {
            var header = new HttpHeader();
            await header.ParseHeaderAsync(stream, HttpAction.Request);
            var destination = await FindDestinationByHeadersAsync(header, from, cancellationToken);
            var count = header.WriteToMemory(HttpAction.Request, buffer);
            return new RouteResult
            {
                HeaderLength = count,
                IsSucceeded = destination != null,
                Identifier = destination
            };
        }

        public virtual async ValueTask<string> FindDestinationByHeadersAsync(
            HttpHeader headers,
            IPEndPoint from, 
            CancellationToken cancellationToken = default) 
                => null;
    }
}
