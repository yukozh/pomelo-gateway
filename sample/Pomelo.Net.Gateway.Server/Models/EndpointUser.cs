using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pomelo.Net.Gateway.Server.Models
{
    public class EndpointUser
    {
        [ForeignKey(nameof(Endpoint))]
        public Guid EndpointId { get; set; }

        public virtual Endpoint Endpoint { get; set; }

        [MaxLength(64)]
        [ForeignKey(nameof(User))]
        public string UserId { get; set; }

        public virtual User User { get; set; }
    }
}
