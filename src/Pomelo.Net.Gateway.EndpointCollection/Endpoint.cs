using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public enum Protocol : byte
    { 
        TCP,
        UDP
    }

    public class Endpoint
    {
        public Guid Id { get; set; }

        public Protocol Protocol { get; set; }

        [MaxLength(256)]
        public string Address { get; set; }

        public ushort Port { get; set; }

        public Guid RouterId { get; set; }

        public Guid TunnelId { get; set; }

        public virtual ICollection<EndpointUser> Users { get; set; } = new List<EndpointUser>();

        [NotMapped]
        public IPAddress IPAddress 
        {
            get => IPAddress.Parse(Address);
            set => Address = value.ToString();
        }
    }
}
