using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pomelo.Net.Gateway.Server.Models
{
    public class InitiativeRule
    {
        [MaxLength(256)]
        public string Id { get; set; }

        [ForeignKey(nameof(Endpoint))]
        public Guid EndpointId { get; set; }

        public virtual Endpoint Endpoint { get; set; }

        [MaxLength(256)]
        public string DestinationEndpoint { get; set; }
    }
}
