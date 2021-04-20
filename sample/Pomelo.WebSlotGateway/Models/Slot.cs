using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Pomelo.WebSlotGateway.Models
{
    public enum SlotStatus
    { 
        Enabled,
        Disabled,
        Error
    }

    public enum DestinationType
    { 
        Http,
        Https
    }

    public class Slot
    {
        public Guid Id { get; set; }

        [MaxLength(64)]
        public string Name { get; set; }

        [MaxLength(128)]
        public string Host { get; set; }

        [MaxLength(128)]
        public string Destination { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public DestinationType DestinationType { get; set; }

        public uint Priority { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public SlotStatus Status { get; set; }
    }
}
