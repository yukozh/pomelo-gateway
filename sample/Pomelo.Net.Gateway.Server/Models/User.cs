using System.ComponentModel.DataAnnotations;

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

        public UserRole Role { get; set; }
    }
}
