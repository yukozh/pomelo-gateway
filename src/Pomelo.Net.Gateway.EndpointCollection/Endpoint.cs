using System;
using System.Collections.Generic;
using System.Net;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public enum Protocol : byte
    { 
        TCP,
        UDP
    }

    public enum EndpointType
    { 
        Bridge,
        Static
    }

    public class EndPoint
    {
        public Guid Id { get; set; }

        public Protocol Protocol { get; set; }

        public Guid RouterId { get; set; }

        public Guid TunnelId { get; set; }

        public HashSet<string> UserIds { get; set; } = new HashSet<string>();

        public EndpointType Type { get; set; }

        public IPEndPoint ListenerEndPoint { get; set; }
    }
}
