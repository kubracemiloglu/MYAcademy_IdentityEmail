using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MYAcademy_IdentityEmail.Context;
using MYAcademy_IdentityEmail.Entities;
using MYAcademy_IdentityEmail.Helpers;
using MYAcademy_IdentityEmail.Models;

namespace MYAcademy_IdentityEmail.Controllers
{
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly EmailContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;
        public AccountController(UserManager<AppUser> userManager, EmailContext context, IConfiguration configuration, ILogger<AccountController> logger)
        { _userManager = userManager; _context = context; _configuration = configuration; _logger = logger; }

        public IActionResult VerifyEmail(string userId) => View(new VerifyEmailViewModel { UserId = userId });

        [HttpPost]
        public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.FindByIdAsync(model.UserId);
            var state = await _context.EmailVerificationStates.SingleOrDefaultAsync(x => x.UserId == model.UserId);
            var valid = user != null && !user.EmailConfirmed && user.ValidCode != null && state != null && state.ExpiresAt > DateTime.UtcNow && state.FailedAttempts < 5;
            if (valid)
            {
                state!.FailedAttempts++;
                var matches = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(user!.ValidCode!.Value.ToString()), Encoding.UTF8.GetBytes(model.Code));
                if (matches)
                {
                    user.EmailConfirmed = true;
                    user.ValidCode = null;
                    var result = await _userManager.UpdateAsync(user);
                    if (!result.Succeeded) { ModelState.AddModelError("", "Hesabınız doğrulanamadı. Yeniden deneyin."); return View(model); }
                    state.ExpiresAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "E-posta adresiniz doğrulandı. Giriş yapabilirsiniz.";
                    return RedirectToAction("SignIn", "Login");
                }
                await _context.SaveChangesAsync();
            }
            ModelState.AddModelError(nameof(model.Code), "Kod geçersiz, süresi dolmuş veya deneme sınırına ulaşılmış. Yeni bir kod isteyin.");
            return View(model);
        }

        public IActionResult ResendVerification() => View(new EmailRequestViewModel());

        [HttpPost]
        public async Task<IActionResult> ResendVerification(EmailRequestViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (user != null && !user.EmailConfirmed)
            {
                var state = await _context.EmailVerificationStates.SingleOrDefaultAsync(x => x.UserId == user.Id);
                if (state == null || state.LastSentAt <= DateTime.UtcNow.AddMinutes(-1))
                {
                    // Dersteki 6 haneli kod üretimi ve AppUser.ValidCode alanı korunur.
                    user.ValidCode = new Random().Next(100000, 999999);
                    var result = await _userManager.UpdateAsync(user);
                    if (result.Succeeded)
                    {
                        if (state == null) { state = new EmailVerificationState { UserId = user.Id }; _context.EmailVerificationStates.Add(state); }
                        state.LastSentAt = DateTime.UtcNow; state.ExpiresAt = DateTime.UtcNow.AddMinutes(10); state.FailedAttempts = 0;
                        await _context.SaveChangesAsync();
                        try
                        {
                            var origin = _configuration["App:PublicUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                            var verifyUrl = origin.TrimEnd('/') + Url.Action(nameof(VerifyEmail), "Account", new { userId = user.Id });
                            await VeloraMail.SendAsync(_configuration, user.Email!, user.Name, "VELORA Mail · E-posta doğrulama", "Doğrulama kodunuz: " + user.ValidCode + "\n" + verifyUrl,
                                VeloraMail.VerificationHtml(user.Name, user.ValidCode.Value.ToString(), state.ExpiresAt, verifyUrl));
                        }
                        catch (Exception exception) when (IsMailFailure(exception))
                        { _logger.LogWarning("Doğrulama e-postası gönderilemedi: {ErrorType}", exception.GetType().Name); }
                    }
                }
            }
            TempData["Notice"] = "Doğrulanmamış bir hesabınız varsa ve yeniden gönderim süresi dolduysa e-postanıza bir kod gönderilir. E-postadaki doğrulama bağlantısı üzerinden kodunuzu girebilirsiniz.";
            // Kayıt durumunu ve kullanıcı kimliğini anonim isteğe açıklama.
            return RedirectToAction(nameof(VerificationRequested));
        }

        public IActionResult VerificationRequested() => View();
        public IActionResult ForgotPassword() => View(new EmailRequestViewModel());

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(EmailRequestViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (user?.EmailConfirmed == true)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var origin = _configuration["App:PublicUrl"];
                if (string.IsNullOrWhiteSpace(origin)) origin = $"{Request.Scheme}://{Request.Host}";
                var path = Url.Action(nameof(ResetPassword), "Account", new { userId = user.Id, token });
                var url = origin.TrimEnd('/') + path;
                try { await VeloraMail.SendAsync(_configuration, user.Email!, user.Name, "VELORA Mail · Şifre sıfırlama", "Şifrenizi yenilemek için: " + url, VeloraMail.ResetHtml(user.Name, url, DateTime.UtcNow.AddHours(1))); }
                catch (Exception exception) when (IsMailFailure(exception)) { _logger.LogWarning("Şifre sıfırlama e-postası gönderilemedi: {ErrorType}", exception.GetType().Name); }
            }
            return RedirectToAction(nameof(PasswordResetRequested));
        }

        public IActionResult PasswordResetRequested() => View();
        public IActionResult ResetPassword(string userId, string token) => View(new ResetPasswordViewModel { UserId = userId, Token = token });

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.FindByIdAsync(model.UserId);
            var result = user == null ? IdentityResult.Failed() : await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (!result.Succeeded) { ModelState.AddModelError("", "Bağlantı geçersiz/süresi dolmuş veya şifre politikasına uygun değil. Yeni bir bağlantı isteyin; şifrenizde büyük harf, küçük harf, rakam ve özel karakter kullanın."); return View(model); }
            TempData["Success"] = "Şifreniz yenilendi. Yeni şifrenizle giriş yapabilirsiniz.";
            return RedirectToAction("SignIn", "Login");
        }

        private static bool IsMailFailure(Exception exception) => exception is MailKit.CommandException or MailKit.ProtocolException or IOException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException or InvalidOperationException;
    }
}
