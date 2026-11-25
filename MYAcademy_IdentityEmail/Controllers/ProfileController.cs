using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MYAcademy_IdentityEmail.Entities;
using MYAcademy_IdentityEmail.Models;
using MYAcademy_IdentityEmail.Helpers;

namespace MYAcademy_IdentityEmail.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IWebHostEnvironment _environment;
        public ProfileController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, IWebHostEnvironment environment)
        { _userManager = userManager; _signInManager = signInManager; _environment = environment; }
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            return user == null ? Challenge() : View(new ProfileViewModel { Name = user.Name, Surname = user.Surname, Email = user.Email, UserName = user.UserName, ImageUrl = user.ImageUrl });
        }

        [HttpPost]
        [RequestSizeLimit(3 * 1024 * 1024)]
        public async Task<IActionResult> Index(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            model.Email = user.Email; model.UserName = user.UserName; model.ImageUrl = user.ImageUrl;
            byte[]? photoBytes = null;
            string? photoExtension = null;
            if (model.Photo != null)
            {
                if (model.Photo.Length is <= 0 or > 2 * 1024 * 1024) ModelState.AddModelError(nameof(model.Photo), "Fotoğraf en fazla 2 MB olabilir.");
                else
                {
                    using var buffer = new MemoryStream(); await model.Photo.CopyToAsync(buffer); photoBytes = buffer.ToArray();
                    photoExtension = ProfilePhotoValidator.ValidExtension(photoBytes, model.Photo.ContentType);
                    if (photoExtension == null) ModelState.AddModelError(nameof(model.Photo), "Yalnızca geçerli PNG veya JPEG fotoğraf yükleyin. Fotoğraf en fazla 4096 × 4096 piksel olabilir.");
                }
            }
            if (!ModelState.IsValid) return View(model);
            if (photoBytes != null && photoExtension != null)
            {
                var directory = Path.Combine(_environment.WebRootPath, "uploads", "avatars"); Directory.CreateDirectory(directory);
                var filename = Guid.NewGuid().ToString("N") + photoExtension;
                await System.IO.File.WriteAllBytesAsync(Path.Combine(directory, filename), photoBytes);
                user.ImageUrl = "/uploads/avatars/" + filename;
            }
            user.Name = model.Name.Trim(); user.Surname = model.Surname.Trim();
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) { foreach (var error in result.Errors) ModelState.AddModelError("", error.Description); return View(model); }
            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Profil bilgileriniz güncellendi."; return RedirectToAction(nameof(Index));
        }

        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());
        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.Password);
            if (!result.Succeeded) { foreach (var error in result.Errors) ModelState.AddModelError("", error.Description); return View(model); }
            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Şifreniz değiştirildi."; return RedirectToAction(nameof(Index));
        }
    }
}
