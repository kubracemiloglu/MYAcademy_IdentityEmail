using MailKit;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using MYAcademy_IdentityEmail.Entities;
using MYAcademy_IdentityEmail.Models;
using Microsoft.AspNetCore.Authorization;
using MYAcademy_IdentityEmail.Context;
using MYAcademy_IdentityEmail.Helpers;

namespace MYAcademy_IdentityEmail.Controllers
{
    [AllowAnonymous]
    public class RegisterController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly EmailContext _context;
        private readonly ILogger<RegisterController> _logger;

        public RegisterController(UserManager<AppUser> userManager, IConfiguration configuration, EmailContext context, ILogger<RegisterController> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        public IActionResult SignUp()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> SignUp(UserRegisterViewModel userRegisterViewModel)
        {
            if (!ModelState.IsValid) return View(userRegisterViewModel);
            Random random = new Random();
            int validationCode = random.Next(100000, 999999);

            AppUser appUser = new AppUser()
            {
                UserName = userRegisterViewModel.UserName,
                Email = userRegisterViewModel.Email,
                Name = userRegisterViewModel.Name,
                Surname = userRegisterViewModel.Surname,
                ValidCode= validationCode
            };
            var result = await _userManager.CreateAsync(appUser, userRegisterViewModel.Password);
            if (result.Succeeded)
            {
                var expiresAt = DateTime.UtcNow.AddMinutes(10);
                _context.EmailVerificationStates.Add(new EmailVerificationState { UserId = appUser.Id, ExpiresAt = expiresAt, LastSentAt = DateTime.UtcNow });
                await _context.SaveChangesAsync();
                try
                {
                MimeMessage minemessage = new MimeMessage();
                MailboxAddress mailboxAddressFrom = new MailboxAddress("VELORA Mail", _configuration["Smtp:FromEmail"] ?? _configuration["Smtp:UserName"] ?? throw new InvalidOperationException("SMTP gönderen ayarı eksik."));
                minemessage.From.Add(mailboxAddressFrom);

                MailboxAddress mailboxAddressTo = new MailboxAddress(userRegisterViewModel.Name + " " + userRegisterViewModel.Surname, userRegisterViewModel.Email);
                minemessage.To.Add(mailboxAddressTo);
                minemessage.Subject = " Email Doğrulama";
                var bodyBuilder = new BodyBuilder();
                bodyBuilder.TextBody = "Doğrulama Kodunuz: " + validationCode;
                var origin = _configuration["App:PublicUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                var verifyUrl = origin.TrimEnd('/') + Url.Action("VerifyEmail", "Account", new { userId = appUser.Id });
                bodyBuilder.HtmlBody = VeloraMail.VerificationHtml(appUser.Name, validationCode.ToString(), expiresAt, verifyUrl);
                minemessage.Body = bodyBuilder.ToMessageBody();
                using SmtpClient smtpClient = new SmtpClient();
                smtpClient.Connect(_configuration["Smtp:Host"] ?? "smtp.gmail.com", _configuration.GetValue<int?>("Smtp:Port") ?? 587, MailKit.Security.SecureSocketOptions.StartTls);
                smtpClient.Authenticate(
                    _configuration["Smtp:UserName"] ?? throw new InvalidOperationException("Smtp:UserName ayarı eksik."),
                    _configuration["Smtp:Password"] ?? throw new InvalidOperationException("Smtp:Password ayarı eksik."));
                smtpClient.Send(minemessage);
                smtpClient.Disconnect(true);
                }
                catch (Exception exception) when (exception is MailKit.CommandException or MailKit.ProtocolException or System.IO.IOException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException or InvalidOperationException)
                {
                    _logger.LogWarning("Kayıt doğrulama e-postası gönderilemedi: {ErrorType}", exception.GetType().Name);
                    TempData["Notice"] = "Hesabınız oluşturuldu, ancak doğrulama e-postası gönderilemedi. SMTP ayarlarını kontrol edip kodu yeniden isteyin.";
                }

                return RedirectToAction("VerifyEmail", "Account", new { userId = appUser.Id });
            }
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(userRegisterViewModel);
        }
    }
}
