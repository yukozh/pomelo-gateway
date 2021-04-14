using System.Collections.Generic;

namespace Pomelo.WebSlotGateway.Models
{
    public class SetConfigurationRequestViewModel
    {
        public IEnumerable<Config> Configurations { get; set; }
    }
}
