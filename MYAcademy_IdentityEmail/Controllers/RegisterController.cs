using MailKit;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using MYAcademy_IdentityEmail.Entities;
using MYAcademy_IdentityEmail.Models;

namespace MYAcademy_IdentityEmail.Controllers
{
    public class RegisterController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;

        public RegisterController(UserManager<AppUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        public IActionResult SignUp()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> SignUp(UserRegisterViewModel userRegisterViewModel)
        {
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
                MimeMessage minemessage = new MimeMessage();
                MailboxAddress mailboxAddressFrom = new MailboxAddress("Identity Postane", "emailadresiniz");
                minemessage.From.Add(mailboxAddressFrom);

                MailboxAddress mailboxAddressTo = new MailboxAddress(userRegisterViewModel.Name + " " + userRegisterViewModel.Surname, userRegisterViewModel.Email);
                minemessage.To.Add(mailboxAddressTo);
                minemessage.Subject = " Email Doğrulama";
                var bodyBuilder = new BodyBuilder();
                bodyBuilder.TextBody = "Doğrulama Kodunuz: " + validationCode;
                minemessage.Body = bodyBuilder.ToMessageBody();
                SmtpClient smtpClient = new SmtpClient();
                smtpClient.Connect("smtp.gmail.com", 587, false);
                smtpClient.Authenticate(
                    _configuration["Smtp:UserName"] ?? throw new InvalidOperationException("Smtp:UserName ayarı eksik."),
                    _configuration["Smtp:Password"] ?? throw new InvalidOperationException("Smtp:Password ayarı eksik."));
                smtpClient.Send(minemessage);
                smtpClient.Disconnect(true);

                return RedirectToAction("SignIn", "Login");
            }
            return View();
        }
    }
}
