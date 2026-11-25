using System.ComponentModel.DataAnnotations;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Models
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Adınızı yazın.")][StringLength(80)] public string Name { get; set; } = string.Empty;
        [Required(ErrorMessage = "Soyadınızı yazın.")][StringLength(80)] public string Surname { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public string? ImageUrl { get; set; }
        public IFormFile? Photo { get; set; }
    }
}
