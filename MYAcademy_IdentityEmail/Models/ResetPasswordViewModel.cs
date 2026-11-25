using System.ComponentModel.DataAnnotations;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Models
{
    public class ResetPasswordViewModel
    {
        [Required] public string UserId { get; set; } = string.Empty;
        [Required] public string Token { get; set; } = string.Empty;
        [Required(ErrorMessage = "Yeni şifrenizi yazın.")][DataType(DataType.Password)][StringLength(128, MinimumLength = 6, ErrorMessage = "Şifre 6–128 karakter olmalı.")]
        public string Password { get; set; } = string.Empty;
        [DataType(DataType.Password)][Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
