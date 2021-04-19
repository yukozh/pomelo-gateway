﻿using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Http
{
    public class HttpTunnelContext : IDisposable
    {
        public Guid ConnectionId { get; set; }

        public HttpTunnelContextPart Request { get; private set; } = new HttpTunnelContextPart { HttpAction = HttpAction.Request };

        public HttpTunnelContextPart Response { get; private set; } = new HttpTunnelContextPart { HttpAction = HttpAction.Response };

        public StreamTunnelContext StreamTunnelContext { get; set; }

        public void Dispose()
        {
            Request?.Body?.Dispose();
            Response?.Body?.Dispose();
        }
    }

    public class HttpTunnelContextPart : IDisposable
    {
        public Stream SourceStream { get; set; }

        public Stream DestinationStream { get; set; }

        public HttpHeader Headers { get; set; }

        public HttpBodyReadonlyStream Body { get; internal set; } = null;

        public HttpAction HttpAction { get; internal set; }

        public void Dispose()
        {
            Body?.Dispose();
        }
    }
}
