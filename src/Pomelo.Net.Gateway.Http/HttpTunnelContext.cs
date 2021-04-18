using System;
using System.IO;

namespace Pomelo.Net.Gateway.Http
{
    public class HttpTunnelContext
    {
        public Guid ConnectionId { get; set; }

        public Stream RequestSourceStream { get; set; }

        public Stream RequestDestinationStream { get; set; }

        public Stream ResponseSourceStream { get; set; }

        public Stream ResponseDestinationStream { get; set; }

        public HttpHeader RequestHeaders { get; set; }

        public HttpHeader ResponseHeaders { get; set; }
    }
}
