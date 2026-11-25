using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MYAcademy_IdentityEmail.Entities;
using MYAcademy_IdentityEmail.Models;

namespace MYAcademy_IdentityEmail.Controllers
{
    public class LoginController : Controller
    {
        private readonly SignInManager<AppUser> _signInManager;
        public LoginController(SignInManager<AppUser> signInManager)
        {
            _signInManager = signInManager;
        }
        [AllowAnonymous]
        public IActionResult SignIn()
        {
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SignIn(UserLoginViewModel userLoginViewModel)
        {
            if (!ModelState.IsValid) return View(userLoginViewModel);
            var result = await _signInManager.PasswordSignInAsync(userLoginViewModel.UserName, userLoginViewModel.Password, userLoginViewModel.RememberMe, true);
            if (result.Succeeded)
            {
                return RedirectToAction("Inbox", "Message");
            }
            ModelState.AddModelError("", result.IsLockedOut ? "Çok fazla başarısız deneme. 15 dakika sonra yeniden deneyin."
                : result.IsNotAllowed ? "Hesabınıza giriş yapmadan önce e-posta adresinizi doğrulayın."
                : "Kullanıcı adı veya şifre hatalı.");
            return View(userLoginViewModel);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SignOutAccount()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(SignIn));
        }
    }
}
