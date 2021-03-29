using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class EndpointUser
    {
        [ForeignKey("Endpoint")]
        public Guid EndpointId { get; set; }

        public virtual Endpoint Endpoint { get; set; }

        [MaxLength(256)]
        public string UserIdentifier { get; set; }
    }
}
