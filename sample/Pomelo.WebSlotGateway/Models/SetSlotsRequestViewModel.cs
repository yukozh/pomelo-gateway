using System.Collections.Generic;

namespace Pomelo.WebSlotGateway.Models
{
    public class SetSlotsRequestViewModel
    {
        public IEnumerable<Slot> Slots { get; set; }
    }
}
