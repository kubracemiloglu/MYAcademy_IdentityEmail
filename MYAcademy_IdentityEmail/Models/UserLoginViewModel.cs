namespace MYAcademy_IdentityEmail.Models
{
    using System.ComponentModel.DataAnnotations;

    public class UserLoginViewModel
    {
        [Required(ErrorMessage = "Kullanıcı adınızı yazın.")]
        public string UserName { get; set; }
        [Required(ErrorMessage = "Şifrenizi yazın.")][DataType(DataType.Password)]
        public string Password { get; set; }
        public bool RememberMe { get; set; }
    }
}
