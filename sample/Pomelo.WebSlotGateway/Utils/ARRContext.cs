using System;
using System.Net;

namespace Pomelo.WebSlotGateway.Utils
{
    public class ARRContext
    {
        public IPAddress ClientAddress { get; set; }
        public DateTime CreatedTimeUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastActiveTimeUtc { get; set; } = DateTime.UtcNow;
        public Guid SlotId { get; set; }
    }
}
