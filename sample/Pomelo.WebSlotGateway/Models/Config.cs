using System.ComponentModel.DataAnnotations;

namespace Pomelo.WebSlotGateway.Models
{
    public class Config
    {
        [Key]
        public string Key { get; set; }

        [MaxLength(64)]
        public string Value { get; set; }

        public string Description { get; set; }
    }
}
