using System.ComponentModel.DataAnnotations;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Mevcut şifrenizi yazın.")][DataType(DataType.Password)] public string CurrentPassword { get; set; } = string.Empty;
        [Required(ErrorMessage = "Yeni şifrenizi yazın.")][DataType(DataType.Password)][StringLength(128, MinimumLength = 6, ErrorMessage = "Şifre 6–128 karakter olmalı.")]
        public string Password { get; set; } = string.Empty;
        [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")][DataType(DataType.Password)] public string ConfirmPassword { get; set; } = string.Empty;
    }
}
