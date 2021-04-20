using System;
using System.ComponentModel.DataAnnotations;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class PreCreateEndpoint
    {
        [Key]
        [MaxLength(256)]
        public string Identifier { get; set; }

        public Protocol Protocol { get; set; }

        [MaxLength(256)]
        public string ServerEndpoint { get; set; }

        [MaxLength(256)]
        public string DestinationEndpoint { get; set; }

        public Guid RouterId { get; set; }

        public Guid TunnelId { get; set; }

        public bool DestinationWithSSL { get; set; }
    }
}
