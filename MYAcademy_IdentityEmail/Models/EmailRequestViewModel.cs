using System.ComponentModel.DataAnnotations;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Models
{
    public class EmailRequestViewModel
    {
        [Required(ErrorMessage = "E-posta adresinizi yazın.")][EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi yazın.")][StringLength(256)]
        public string Email { get; set; } = string.Empty;
    }
}
