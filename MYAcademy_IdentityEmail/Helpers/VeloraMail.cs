using System.Net;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace MYAcademy_IdentityEmail.Helpers
{
    public static class VeloraMail
    {
        public static string VerificationHtml(string name, string code, DateTime expiresAt, string? verifyUrl = null) => Frame(
            "E-posta adresini doğrula", name,
            "VELORA Mail hesabını kullanmaya başlamak için aşağıdaki doğrulama kodunu gir.",
            $"<p style=\"font-size:36px;letter-spacing:10px;font-weight:800;color:#25254D;background:#D5F45B;padding:20px;border-radius:12px;text-align:center\">{WebUtility.HtmlEncode(code)}</p>" + (verifyUrl == null ? "" : $"<p><a style=\"color:#6131C6\" href=\"{WebUtility.HtmlEncode(verifyUrl)}\">Doğrulama ekranını aç</a></p>"),
            "Kodun son kullanım zamanı: " + expiresAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm") + ". Kodu kimseyle paylaşma.");

        public static string ResetHtml(string name, string url, DateTime expiresAt) => Frame(
            "Şifreni yenile", name, "Hesabın için bir şifre sıfırlama talebi aldık.",
            $"<p style=\"margin:30px 0\"><a href=\"{WebUtility.HtmlEncode(url)}\" style=\"background:#D5F45B;color:#161B1A;padding:16px 24px;border-radius:10px;text-decoration:none;font-weight:700;display:inline-block\">Yeni şifre belirle</a></p>",
            "Bağlantının son kullanım zamanı: " + expiresAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm") + ". Bu talebi sen yapmadıysan bu e-postayı yok sayabilirsin.");

        private static string Frame(string title, string name, string text, string content, string footer) => $"""
            <!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="color-scheme" content="light dark"><title>{WebUtility.HtmlEncode(title)}</title></head>
            <body style="margin:0;background:#F5F4EF;font-family:Arial,Helvetica,sans-serif;color:#161B1A">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr><td align="center" style="padding:28px 12px">
            <table role="presentation" width="600" cellspacing="0" cellpadding="0" style="width:100%;max-width:600px;background:#FFFFFF;border-radius:20px;overflow:hidden">
            <tr><td bgcolor="#25254D" style="padding:28px;color:#FFFFFF"><strong style="font-size:28px">VELORA Mail<span style="color:#D5F45B">.</span></strong><p style="font-size:12px;margin:6px 0 0">Communication Without Noise.</p></td></tr>
            <tr><td style="padding:32px;color:#161B1A;background:#FFFFFF"><h1 style="font-size:26px;color:#25254D">{WebUtility.HtmlEncode(title)}</h1><p>Merhaba {WebUtility.HtmlEncode(name)},</p><p style="line-height:1.7">{WebUtility.HtmlEncode(text)}</p>{content}<p style="font-size:13px;line-height:1.7;color:#555568">{WebUtility.HtmlEncode(footer)}</p></td></tr>
            <tr><td bgcolor="#25254D" style="padding:20px 32px;color:#FFFFFF;font-size:12px">VELORA Mail · Daha sakin bir iletişim alanı.</td></tr>
            </table></td></tr></table></body></html>
            """;

        public static async Task SendAsync(IConfiguration configuration, string email, string name, string subject, string text, string html)
        {
            var username = configuration["Smtp:UserName"] ?? throw new InvalidOperationException("SMTP kullanıcı ayarı eksik.");
            var password = configuration["Smtp:Password"] ?? throw new InvalidOperationException("SMTP şifre ayarı eksik.");
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("VELORA Mail", configuration["Smtp:FromEmail"] ?? username));
            message.To.Add(new MailboxAddress(name, email));
            message.Subject = subject;
            message.Body = new BodyBuilder { TextBody = text, HtmlBody = html }.ToMessageBody();
            using var client = new SmtpClient();
            await client.ConnectAsync(configuration["Smtp:Host"] ?? "smtp.gmail.com", configuration.GetValue<int?>("Smtp:Port") ?? 587, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
