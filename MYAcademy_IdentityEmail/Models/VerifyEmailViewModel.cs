using System.ComponentModel.DataAnnotations;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Models
{
    public class VerifyEmailViewModel
    {
        [Required] public string UserId { get; set; } = string.Empty;
        [Required(ErrorMessage = "E-postanıza gelen kodu yazın.")][RegularExpression("^[0-9]{6}$", ErrorMessage = "Doğrulama kodu 6 haneli olmalı.")]
        public string Code { get; set; } = string.Empty;
    }
}
