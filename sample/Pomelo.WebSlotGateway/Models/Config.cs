using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Pomelo.WebSlotGateway.Models
{
    public enum ConfigType
    { 
        Text,
        Password,
        DropDownList
    }

    public class Config
    {
        [Key]
        public string Key { get; set; }

        [MaxLength(64)]
        public string Value { get; set; }

        public string Description { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public ConfigType Type { get; set; }

        public string Addition { get; set; }
    }
}
