using System.ComponentModel.DataAnnotations;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.Server.Models
{
    public class PublicRule
    {
        [MaxLength(256)]
        public string Id { get; set; }

        public Protocol Protocol { get; set; }

        [MaxLength(256)]
        public string ServerEndpoint { get; set; }

        [MaxLength(256)]
        public string DestinationEndpoint { get; set; }
    }
}
