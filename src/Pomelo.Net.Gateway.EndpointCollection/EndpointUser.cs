﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class EndpointUser
    {
        [Key]
        [ForeignKey("Endpoint")]
        public Guid EndpointId { get; set; }

        public virtual Endpoint Endpoint { get; set; }

        [Key]
        [MaxLength(256)]
        public string UserIdentifier { get; set; }
    }
}
