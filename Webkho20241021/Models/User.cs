using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Webkho_20241021.Models
{
    public class User : IdentityUser
    {
        public string? Name { get; set; } 

        [Required] 
        public string? manv { get; set; }  

        public string? Chucvu { get; set; }

        public string? Bophan { get; set; }

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }
    }
}
