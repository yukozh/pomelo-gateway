using System;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class MappingRule
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public Protocol Protocol { get; set; }
        public string LocalEndpoint { get; set; }
        public bool LocalWithSSL { get; set; }
        public IPEndPoint RemoteEndpoint { get; set; }
        public Guid LocalTunnelId { get; set; }
        public Guid RemoteTunnelId { get; set; }
        public Guid RemoteRouterId { get; set; }
    }

    public class MappingRule2
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public Protocol Protocol { get; set; }
        public string LocalEndpoint { get; set; }
        public bool LocalWithSSL { get; set; }
        public string RemoteEndpoint { get; set; }
        public Guid LocalTunnelId { get; set; }
        public Guid RemoteTunnelId { get; set; }
        public Guid RemoteRouterId { get; set; }
    }
}
