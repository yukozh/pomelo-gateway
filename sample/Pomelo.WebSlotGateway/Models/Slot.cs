using System;
using System.ComponentModel.DataAnnotations;

namespace Pomelo.WebSlotGateway.Models
{
    public class Slot
    {
        public Guid Id { get; set; }

        [MaxLength(64)]
        public string Name { get; set; }

        [MaxLength(128)]
        public string Destination { get; set; }

        public uint Priority { get; set; }
    }
}
