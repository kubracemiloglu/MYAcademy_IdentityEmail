namespace MYAcademy_IdentityEmail.Models
{
    using System.ComponentModel.DataAnnotations;

    public class UserRegisterViewModel
    {
        [Required(ErrorMessage = "Adınızı yazın.")][StringLength(80)]
        public string Name { get; set; }
        [Required(ErrorMessage = "Soyadınızı yazın.")][StringLength(80)]
        public string Surname { get; set; }
        [Required(ErrorMessage = "Kullanıcı adınızı yazın.")][StringLength(80)]
        public string UserName { get; set; }
        [Required(ErrorMessage = "E-posta adresinizi yazın.")][EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi yazın.")][StringLength(256)]
        public string Email { get; set; }
        [Required(ErrorMessage = "Şifrenizi yazın.")][DataType(DataType.Password)][StringLength(128, MinimumLength = 6, ErrorMessage = "Şifre 6–128 karakter olmalı.")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Şifrenizi tekrar yazın.")][DataType(DataType.Password)][Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
        public string ConfirmPassword { get; set; }
    }
}
