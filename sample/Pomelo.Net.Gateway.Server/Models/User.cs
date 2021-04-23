using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Pomelo.Net.Gateway.Server.Models
{
    public enum UserRole
    { 
        User,
        Admin
    }

    public class User
    {
        [Key]
        [MaxLength(64)]
        public string Username { get; set; }

        [MaxLength(64)]
        public string Password { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public UserRole Role { get; set; }

        public virtual ICollection<EndpointUser> AllowedEndpoints { get; set; } 
            = new List<EndpointUser>();

        public bool AllowCreateOnDemandEndpoint { get; set; }
    }
}
