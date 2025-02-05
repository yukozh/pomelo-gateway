using System;
using System.Net;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class StaticRule
    {
        public string Id { get; set; }

        public Protocol Protocol { get; set; }

        public IPEndPoint ListenerEndpoint { get; set; }

        public IPEndPoint DestinationEndpoint { get; set; }

        public Guid RouterId { get; set; }

        public Guid TunnelId { get; set; }

        public bool UnwrapSsl { get; set; }
    }
}
