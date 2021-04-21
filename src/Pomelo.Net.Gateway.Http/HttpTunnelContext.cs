using System;
using System.IO;
using System.Net;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Http
{
    public class HttpTunnelContext : IDisposable
    {
        public HttpTunnelContext(Guid connectionId)
        {
            ConnectionId = connectionId;
        }

        public Guid ConnectionId { get; internal set; }

        private HttpTunnelContextPart request;
        public HttpTunnelContextPart Request
        {
            get
            {
                if (request == null)
                {
                    request = new HttpTunnelContextPart(this) { HttpAction = HttpAction.Request };
                }
                return request;
            }
        }

        private HttpTunnelContextPart response;
        public HttpTunnelContextPart Response
        {
            get
            {
                if (response == null)
                {
                    response = new HttpTunnelContextPart(this) { HttpAction = HttpAction.Response };
                }
                return response;
            }
        }

        public StreamTunnelContext StreamTunnelContext { get; internal set; }

        public IPEndPoint ClientEndPoint => StreamTunnelContext?.LeftEndpoint;
        public void Dispose()
        {
            Request?.Dispose();
            Response?.Dispose();
        }
    }

    public class HttpTunnelContextPart : IDisposable
    {
        public HttpTunnelContextPart(HttpTunnelContext context)
        {
            HttpContext = context;
        }

        public Stream SourceStream { get; set; }

        public Stream DestinationStream { get; set; }

        public HttpHeader Headers { get; internal set; }

        public HttpBodyStream Body { get; internal set; } = null;

        public HttpAction HttpAction { get; internal set; }

        public HttpTunnelContext HttpContext { get; internal set; }

        public string Url => Headers?.Url;

        public string Path => Headers?.Path;

        public UrlEncodedValueCollection Query => Headers?.Query;

        public void Dispose()
        {
            Body?.Dispose();
            HttpContext = null;
        }
    }
}
