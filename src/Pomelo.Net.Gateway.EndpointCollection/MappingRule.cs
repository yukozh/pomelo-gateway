using System;
using System.Net;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class MappingRule
    {
        Protocol Protocol { get; set; }
        public IPEndPoint LocalEndpoint { get; set; }
        public IPEndPoint RemoteEndpoint { get; set; }
        public Guid LocalTunnelId { get; set; }
        public Guid RemoteTunnelId { get; set; }
        public Guid RemoteRouterId { get; set; }
    }
}
