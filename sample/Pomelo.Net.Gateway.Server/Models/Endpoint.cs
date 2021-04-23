using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Pomelo.Net.Gateway.EndpointCollection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Pomelo.Net.Gateway.Server.Models
{
    public class Endpoint
    {
        public Guid Id { get; set; }

        [MaxLength(32)]
        public string Name { get; set; }

        [MaxLength(128)]
        public string Address { get; set; }

        public int Port { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public Protocol Protocol { get; set; } 

        public Guid RouterId { get; set; }

        public Guid TunnelId { get; set; }

        public virtual ICollection<EndpointUser> EndpointUsers { get; set; }
            = new List<EndpointUser>();

        public virtual ICollection<InitiativeRule> InitiativeRules { get; set; }
            = new List<InitiativeRule>();
    }
}
