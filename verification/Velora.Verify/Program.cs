using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MYAcademy_IdentityEmail.Context;
using MYAcademy_IdentityEmail.Entities;
using MYAcademy_IdentityEmail.Helpers;

if (args.Length != 2) throw new ArgumentException("Usage: seed|test|register|cleanup <local fixture JSON>");
var root = Directory.GetCurrentDirectory();
var fixturePath = Path.GetFullPath(args[1]);
var appDirectory = Path.Combine(root, "MYAcademy_IdentityEmail");
var mode = args[0];
Session session;
if (mode == "seed")
{
    session = new Session { Database = "IdentityEmailVeloraChecks_" + Guid.NewGuid().ToString("N"), Password = "Vlr!" + Guid.NewGuid().ToString("N"), BaseUrl = "http://127.0.0.1:5297" };
}
else session = JsonSerializer.Deserialize<Session>(await File.ReadAllTextAsync(fixturePath))!;
if (!Regex.IsMatch(session.Database, "^IdentityEmailVeloraChecks_[a-f0-9]{32}$")) throw new InvalidOperationException("Only generated isolated verification databases are allowed.");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={session.Database};Integrated Security=True;TrustServerCertificate=True;";
var services = new ServiceCollection();
services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
services.AddDbContext<EmailContext>(options => options.UseSqlServer(connection));
services.AddIdentity<AppUser, IdentityRole>().AddEntityFrameworkStores<EmailContext>().AddDefaultTokenProviders();
services.AddDataProtection().SetApplicationName(appDirectory + Path.DirectorySeparatorChar);
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var database = scope.ServiceProvider.GetRequiredService<EmailContext>();
var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
if (mode == "cleanup") { await database.Database.EnsureDeletedAsync(); Console.WriteLine("Isolated verification database removed. Main database untouched."); return; }
if (mode == "seed")
{
    await database.Database.MigrateAsync();
    foreach (var name in new[] { "alice", "bob", "eve", "unverified" })
    {
        var user = new AppUser { UserName = "velora.test." + name, Email = name + "@velora.test", Name = name == "alice" ? "Ada" : name == "bob" ? "Bora" : name == "eve" ? "Ece" : "Doğrulama", Surname = "Test", EmailConfirmed = name != "unverified" };
        var result = await manager.CreateAsync(user, session.Password);
        if (!result.Succeeded) throw new Exception(string.Join(",", result.Errors.Select(x => x.Description)));
        session.Users[name] = user.Id;
    }
    Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
    await File.WriteAllTextAsync(fixturePath, JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Isolated fixture database created: " + session.Database);
    Console.WriteLine("Main users and real SMTP recipients were not used.");
    return;
}

var results = new List<string>();
void Check(bool condition, string name) { if (!condition) throw new Exception(name); results.Add(name); Console.WriteLine("PASS " + name); }
HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(session.BaseUrl) };
async Task<(HttpResponseMessage Response, string Html)> Get(HttpClient client, string path)
{ var response = await client.GetAsync(path); return (response, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync())); }
async Task<HttpResponseMessage> Post(HttpClient client, string tokenPage, string path, Dictionary<string, string> fields)
{
    var (_, html) = await Get(client, tokenPage);
    var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
    if (!token.Success) throw new Exception("Missing CSRF token at " + tokenPage);
    fields["__RequestVerificationToken"] = token.Groups[1].Value;
    return await client.PostAsync(path, new FormUrlEncodedContent(fields));
}
async Task Login(HttpClient client, string name)
{
    var response = await Post(client, "/Login/SignIn", "/Login/SignIn", new() { ["UserName"] = "velora.test." + name, ["Password"] = session.Password, ["RememberMe"] = "true" });
    if (response.StatusCode != HttpStatusCode.Redirect) throw new Exception("Login failed for isolated test user " + name + ": " + WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    if (!response.Headers.TryGetValues("Set-Cookie", out var cookies) || !cookies.Any(x => x.Contains("expires=", StringComparison.OrdinalIgnoreCase))) throw new Exception("Remember-me cookie has no expiry");
}
try
{
    using var anonymous = Client(); using var alice = Client(); using var bob = Client(); using var eve = Client();
    if (mode == "register")
    {
        var username = "velora.registration." + Guid.NewGuid().ToString("N")[..8];
        var fields = new Dictionary<string, string> { ["Name"] = "Kayıt", ["Surname"] = "Test", ["UserName"] = username, ["Email"] = username + "@velora.test", ["Password"] = session.Password, ["ConfirmPassword"] = "Mismatch!Password" };
        var mismatched = await Post(anonymous, "/Register/SignUp", "/Register/SignUp", fields);
        Check(mismatched.StatusCode == HttpStatusCode.OK && !await database.Users.AnyAsync(x => x.UserName == username), "Registration rejects mismatched passwords before creating an account");
        fields["ConfirmPassword"] = session.Password;
        var registered = await Post(anonymous, "/Register/SignUp", "/Register/SignUp", fields);
        database.ChangeTracker.Clear();
        var account = await manager.FindByNameAsync(username);
        Check(registered.StatusCode == HttpStatusCode.Redirect && registered.Headers.Location!.ToString().Contains("/Account/VerifyEmail") && account != null && !account.EmailConfirmed, "Existing registration action creates an unconfirmed account and redirects to verification");
        var state = await database.EmailVerificationStates.AsNoTracking().SingleAsync(x => x.UserId == account!.Id);
        Check(account!.ValidCode is >= 100000 and <= 999999 && state.ExpiresAt > DateTime.UtcNow, "Registration persists the teacher's six-digit code with a real expiry");
        var verifiedRegistration = await Post(anonymous, registered.Headers.Location!.ToString(), "/Account/VerifyEmail", new() { ["UserId"] = account.Id, ["Code"] = account.ValidCode.GetValueOrDefault().ToString() });
        database.ChangeTracker.Clear();
        Check(verifiedRegistration.StatusCode == HttpStatusCode.Redirect && (await manager.FindByIdAsync(account.Id))!.EmailConfirmed, "Registered account completes the real verification form flow");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "velora", "registration-results.json"), JsonSerializer.Serialize(new { passed = results.Count, checks = results, smtp = "delivery intentionally blocked with loopback SMTP; no real recipients", checkedAtUtc = DateTime.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("All registration checks passed; external SMTP delivery was not attempted.");
        return;
    }
    var challenge = await anonymous.GetAsync("/Message/Inbox");
    Check(challenge.StatusCode == HttpStatusCode.Redirect && challenge.Headers.Location!.ToString().Contains("/Login/SignIn"), "Protected inbox requires authentication");
    Check((await anonymous.GetAsync("/")).StatusCode == HttpStatusCode.Redirect, "Root opens the authenticated VELORA workspace flow");
    var blocked = await Post(anonymous, "/Login/SignIn", "/Login/SignIn", new() { ["UserName"] = "velora.test.unverified", ["Password"] = session.Password });
    Check(blocked.StatusCode == HttpStatusCode.OK && WebUtility.HtmlDecode(await blocked.Content.ReadAsStringAsync()).Contains("doğrulayın"), "Unconfirmed account cannot sign in");
    await Login(alice, "alice"); await Login(bob, "bob"); await Login(eve, "eve");
    Check(true, "Confirmed accounts can sign in; remember-me cookie issued");
    var csrf = await alice.PostAsync("/Message/CreateMessage", new FormUrlEncodedContent(new Dictionary<string, string> { ["Subject"] = "No token" }));
    Check(csrf.StatusCode == HttpStatusCode.BadRequest, "POST without antiforgery token is rejected");
    var invalid = await Post(alice, "/Message/CreateMessage", "/Message/CreateMessage", new() { ["ReceiverEmail"] = "nobody@velora.test", ["Subject"] = "Invalid recipient", ["MessageDetail"] = "Rejected" });
    Check(invalid.StatusCode == HttpStatusCode.OK && WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()).Contains("kayıtlı bir kullanıcı bulunamadı"), "Only registered recipients are accepted");
    var subject = "VELORA integration " + Guid.NewGuid().ToString("N")[..8];
    var sent = await Post(alice, "/Message/CreateMessage", "/Message/CreateMessage", new() { ["ReceiverEmail"] = "bob@velora.test", ["Subject"] = subject, ["MessageDetail"] = "Private text <script>alert(1)</script>", ["SenderEmail"] = "eve@velora.test" });
    Check(sent.StatusCode == HttpStatusCode.Redirect, "Internal message send redirects after persistence");
    database.ChangeTracker.Clear();
    var message = await database.Messages.SingleAsync(x => x.Subject == subject);
    session.MessageId = message.MessageId;
    Check(message.SenderEmail == "alice@velora.test" && message.ReceiverEmail == "bob@velora.test" && !message.IsRead && message.SentDate > DateTime.UtcNow.AddMinutes(-2), "Sender and time come from the session and server");
    Check((await Get(bob, "/Message/Inbox")).Html.Contains(subject) && !(await Get(eve, "/Message/Inbox")).Html.Contains(subject), "Inbox and search data are isolated by recipient");
    var detail = await Get(bob, "/Message/MessageDetail/" + message.MessageId);
    Check(detail.Response.StatusCode == HttpStatusCode.OK && detail.Html.Contains("Ada Test") && detail.Html.Contains("Bora Test"), "Message detail shows real sender and recipient names");
    // Get() decodes entities for Turkish assertions; inspect the raw response separately for XSS.
    var rawDetail = await (await bob.GetAsync("/Message/MessageDetail/" + message.MessageId)).Content.ReadAsStringAsync();
    Check(rawDetail.Contains("&lt;script&gt;alert(1)&lt;/script&gt;"), "Razor HTML-encodes the full message body");
    var backLink = Regex.Match(rawDetail, "<a[^>]*href=\"([^\"]*)\"[^>]*aria-label=\"Mesaj listesine dön\"");
    Check(backLink.Success && !backLink.Groups[1].Value.Contains(message.MessageId), "Back-to-list URL clears the selected message route ID");
    Check((await Get(eve, "/Message/MessageDetail/" + message.MessageId)).Response.StatusCode == HttpStatusCode.NotFound, "Third user cannot read another conversation");
    foreach (var operation in new[] { "SetRead", "ToggleStar", "Delete", "SetCategory", "Restore", "PermanentlyDelete" })
    {
        var denied = await Post(eve, "/Message/Inbox", "/Message/" + operation, new() { ["id"] = message.MessageId, ["isRead"] = "true", ["categoryId"] = "1" });
        Check(denied.StatusCode == HttpStatusCode.NotFound, "Third user cannot call " + operation);
    }
    await Post(bob, "/Message/Inbox", "/Message/SetRead", new() { ["id"] = message.MessageId, ["isRead"] = "true" });
    database.ChangeTracker.Clear(); Check((await database.Messages.FindAsync(message.MessageId))!.IsRead, "Read state persists in SQL");
    await Post(bob, "/Message/Inbox", "/Message/SetRead", new() { ["id"] = message.MessageId, ["isRead"] = "false" });
    database.ChangeTracker.Clear(); Check(!(await database.Messages.FindAsync(message.MessageId))!.IsRead, "Mark-unread persists in SQL");
    await Post(bob, "/Message/Inbox", "/Message/ToggleStar", new() { ["id"] = message.MessageId });
    Check((await Get(bob, "/Message/Starred")).Html.Contains(subject) && !(await Get(alice, "/Message/Starred")).Html.Contains(subject), "Stars are personal and have a real filtered list");
    var category = await database.Categories.OrderBy(x => x.CategoryId).FirstAsync();
    await Post(bob, "/Message/Inbox", "/Message/SetCategory", new() { ["id"] = message.MessageId, ["categoryId"] = category.CategoryId.ToString() });
    Check((await Get(bob, "/Message/Category?categoryId=" + category.CategoryId)).Html.Contains(subject) && !(await Get(alice, "/Message/Category?categoryId=" + category.CategoryId)).Html.Contains(subject), "Category assignment belongs only to the active user");
    Check((await Get(bob, "/Message/Inbox?q=" + Uri.EscapeDataString(subject))).Html.Contains(subject) && (await Get(alice, "/Message/Sendbox?q=" + Uri.EscapeDataString(subject))).Html.Contains(subject), "Inbox and sendbox searches return matching real messages");
    var reply = (await Get(bob, "/Message/CreateMessage?replyTo=" + message.MessageId)).Html;
    var forward = (await Get(bob, "/Message/CreateMessage?forwardId=" + message.MessageId)).Html;
    Check(reply.Contains("alice@velora.test") && reply.Contains("Ynt:") && forward.Contains("İlt:"), "Reply and forward populate the existing composer fields");
    await Post(bob, "/Message/Inbox", "/Message/Delete", new() { ["id"] = message.MessageId });
    Check(!(await Get(bob, "/Message/Inbox")).Html.Contains(subject) && (await Get(bob, "/Message/Trash")).Html.Contains(subject) && (await Get(alice, "/Message/Sendbox")).Html.Contains(subject), "Soft-delete does not affect the other participant");
    await Post(bob, "/Message/Trash", "/Message/Restore", new() { ["id"] = message.MessageId });
    Check((await Get(bob, "/Message/Inbox")).Html.Contains(subject), "Trash restore returns the message");
    await Post(bob, "/Message/Inbox", "/Message/Delete", new() { ["id"] = message.MessageId });
    await Post(bob, "/Message/Trash", "/Message/PermanentlyDelete", new() { ["id"] = message.MessageId });
    Check(!(await Get(bob, "/Message/Trash")).Html.Contains(subject) && (await Get(alice, "/Message/Sendbox")).Html.Contains(subject) && await database.Messages.AnyAsync(x => x.MessageId == message.MessageId), "Permanent removal applies only to the acting user's account");
    var saved = await Post(bob, "/Message/CreateMessage", "/Draft/Save", new() { ["Subject"] = "Incomplete draft " + subject });
    Check(saved.StatusCode == HttpStatusCode.Redirect, "Incomplete drafts can be saved");
    database.ChangeTracker.Clear(); var draft = await database.DraftMessages.SingleAsync(x => x.Subject == "Incomplete draft " + subject);
    Check((await Get(alice, "/Draft/Edit/" + draft.DraftMessageId)).Response.StatusCode == HttpStatusCode.NotFound, "Drafts are owner-only");
    var draftSent = await Post(bob, "/Draft/Edit/" + draft.DraftMessageId, "/Message/CreateMessage", new() { ["DraftMessageId"] = draft.DraftMessageId, ["ReceiverEmail"] = "alice@velora.test", ["Subject"] = "Draft sent " + subject, ["MessageDetail"] = "Completed draft" });
    database.ChangeTracker.Clear();
    Check(draftSent.StatusCode == HttpStatusCode.Redirect && !await database.DraftMessages.AnyAsync(x => x.DraftMessageId == draft.DraftMessageId) && await database.Messages.AnyAsync(x => x.Subject == "Draft sent " + subject), "Sending a draft creates a message and removes only that draft");
    await Post(bob, "/Message/CreateMessage", "/Draft/Save", new() { ["Subject"] = "Disposable " + subject });
    database.ChangeTracker.Clear(); var disposableDraft = await database.DraftMessages.SingleAsync(x => x.Subject == "Disposable " + subject);
    await Post(bob, "/Draft", "/Draft/Delete", new() { ["id"] = disposableDraft.DraftMessageId });
    Check(!await database.DraftMessages.AnyAsync(x => x.DraftMessageId == disposableDraft.DraftMessageId), "Draft deletion persists without affecting sent messages");
    var dashboard = await Get(alice, "/Dashboard/Index");
    var chartJson = Regex.Match(dashboard.Html, "<script id=\"veloraTrafficData\" type=\"application/json\">(.*?)</script>", RegexOptions.Singleline);
    using var chart = JsonDocument.Parse(chartJson.Groups[1].Value);
    Check(dashboard.Response.StatusCode == HttpStatusCode.OK && chart.RootElement.GetProperty("labels").GetArrayLength() == 7 && chart.RootElement.GetProperty("received").EnumerateArray().Sum(x => x.GetInt32()) >= 1, "Dashboard includes six SQL-based counts and seven real daily samples");
    var profile = await Post(bob, "/Profile/Index", "/Profile/Index", new() { ["Name"] = "Bora Güncel", ["Surname"] = "Test" });
    database.ChangeTracker.Clear(); Check(profile.StatusCode == HttpStatusCode.Redirect && (await manager.FindByIdAsync(session.Users["bob"]))!.Name == "Bora Güncel", "Profile updates persist and navbar uses the Identity user");
    var photoPage = (await Get(bob, "/Profile/Index")).Html;
    var photoToken = Regex.Match(photoPage, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    using (var invalidPhoto = new MultipartFormDataContent())
    {
        invalidPhoto.Add(new StringContent(photoToken), "__RequestVerificationToken"); invalidPhoto.Add(new StringContent("Bora Güncel"), "Name"); invalidPhoto.Add(new StringContent("Test"), "Surname");
        var badBytes = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("<script>not an image</script>")); badBytes.Headers.ContentType = new("image/png"); invalidPhoto.Add(badBytes, "Photo", "profile.png");
        var rejectedPhoto = await bob.PostAsync("/Profile/Index", invalidPhoto);
        Check(rejectedPhoto.StatusCode == HttpStatusCode.OK && WebUtility.HtmlDecode(await rejectedPhoto.Content.ReadAsStringAsync()).Contains("geçerli PNG"), "MIME-spoofed profile photo is rejected");
    }
    using (var validPhoto = new MultipartFormDataContent())
    {
        validPhoto.Add(new StringContent(photoToken), "__RequestVerificationToken"); validPhoto.Add(new StringContent("Bora Güncel"), "Name"); validPhoto.Add(new StringContent("Test"), "Surname");
        var png = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII=")); png.Headers.ContentType = new("image/png"); validPhoto.Add(png, "Photo", "profile.png");
        var acceptedPhoto = await bob.PostAsync("/Profile/Index", validPhoto);
        database.ChangeTracker.Clear(); var photoUser = await manager.FindByIdAsync(session.Users["bob"]);
        Check(acceptedPhoto.StatusCode == HttpStatusCode.Redirect && photoUser!.ImageUrl!.StartsWith("/uploads/avatars/") && (await bob.GetAsync(photoUser.ImageUrl)).StatusCode == HttpStatusCode.OK, "Valid profile photo uses a generated name and is served locally");
    }
    var passwordChanged = await Post(bob, "/Profile/ChangePassword", "/Profile/ChangePassword", new() { ["CurrentPassword"] = session.Password, ["Password"] = session.Password + "New!", ["ConfirmPassword"] = session.Password + "New!" });
    database.ChangeTracker.Clear(); Check(passwordChanged.StatusCode == HttpStatusCode.Redirect && await manager.CheckPasswordAsync((await manager.FindByIdAsync(session.Users["bob"]))!, session.Password + "New!"), "Password change verifies the current password and refreshes the session");
    var unverified = await manager.FindByIdAsync(session.Users["unverified"]);
    unverified!.ValidCode = 654321; await manager.UpdateAsync(unverified);
    var verification = new EmailVerificationState { UserId = unverified.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(10), LastSentAt = DateTime.UtcNow };
    database.EmailVerificationStates.Add(verification); await database.SaveChangesAsync();
    verification.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); await database.SaveChangesAsync();
    var expired = await Post(anonymous, "/Account/VerifyEmail?userId=" + unverified.Id, "/Account/VerifyEmail", new() { ["UserId"] = unverified.Id, ["Code"] = "654321" });
    Check(expired.StatusCode == HttpStatusCode.OK && WebUtility.HtmlDecode(await expired.Content.ReadAsStringAsync()).Contains("süresi dolmuş"), "Expired verification code is rejected");
    verification.ExpiresAt = DateTime.UtcNow.AddMinutes(10); verification.FailedAttempts = 5; await database.SaveChangesAsync();
    var attemptsBlocked = await Post(anonymous, "/Account/VerifyEmail?userId=" + unverified.Id, "/Account/VerifyEmail", new() { ["UserId"] = unverified.Id, ["Code"] = "654321" });
    Check(attemptsBlocked.StatusCode == HttpStatusCode.OK, "Verification attempt limit rejects even a correct code");
    verification.FailedAttempts = 0; await database.SaveChangesAsync();
    var resend = await Post(anonymous, "/Account/ResendVerification", "/Account/ResendVerification", new() { ["Email"] = unverified.Email! });
    database.ChangeTracker.Clear(); Check(resend.StatusCode == HttpStatusCode.Redirect && (await manager.FindByIdAsync(unverified.Id))!.ValidCode == 654321, "Resend cooldown prevents a second email within one minute");
    var verified = await Post(anonymous, "/Account/VerifyEmail?userId=" + unverified.Id, "/Account/VerifyEmail", new() { ["UserId"] = unverified.Id, ["Code"] = "654321" });
    database.ChangeTracker.Clear(); Check(verified.StatusCode == HttpStatusCode.Redirect && (await manager.FindByIdAsync(unverified.Id))!.EmailConfirmed, "Valid six-digit code confirms the account");
    var resetUser = await manager.FindByIdAsync(session.Users["eve"]);
    var token = await manager.GeneratePasswordResetTokenAsync(resetUser!);
    var resetUrl = "/Account/ResetPassword?userId=" + Uri.EscapeDataString(resetUser!.Id) + "&token=" + Uri.EscapeDataString(token);
    var newPassword = "New!" + Guid.NewGuid().ToString("N");
    var reset = await Post(anonymous, resetUrl, "/Account/ResetPassword", new() { ["UserId"] = resetUser.Id, ["Token"] = token, ["Password"] = newPassword, ["ConfirmPassword"] = newPassword });
    database.ChangeTracker.Clear(); Check(reset.StatusCode == HttpStatusCode.Redirect && await manager.CheckPasswordAsync((await manager.FindByIdAsync(resetUser.Id))!, newPassword), "Identity reset token works through the reset form");
    var reusedReset = await Post(anonymous, resetUrl, "/Account/ResetPassword", new() { ["UserId"] = resetUser.Id, ["Token"] = token, ["Password"] = newPassword + "Again!", ["ConfirmPassword"] = newPassword + "Again!" });
    Check(reusedReset.StatusCode == HttpStatusCode.OK && WebUtility.HtmlDecode(await reusedReset.Content.ReadAsStringAsync()).Contains("Bağlantı geçersiz"), "Password reset token cannot be reused");
    var resetRequest = await Post(anonymous, "/Account/ForgotPassword", "/Account/ForgotPassword", new() { ["Email"] = "missing@velora.test" });
    Check(resetRequest.StatusCode == HttpStatusCode.Redirect && (await Get(anonymous, resetRequest.Headers.Location!.ToString())).Html.Contains("doğrulanmış bir hesabın varsa"), "Reset request does not disclose whether an email is registered");
    var lockUser = new AppUser { UserName = "velora.test.lock." + Guid.NewGuid().ToString("N")[..6], Email = Guid.NewGuid().ToString("N") + "@velora.test", Name = "Lockout", Surname = "Test", EmailConfirmed = true };
    await manager.CreateAsync(lockUser, session.Password);
    for (var attempt = 0; attempt < 5; attempt++) await Post(anonymous, "/Login/SignIn", "/Login/SignIn", new() { ["UserName"] = lockUser.UserName, ["Password"] = "Wrong!Password" });
    database.ChangeTracker.Clear(); Check((await manager.FindByIdAsync(lockUser.Id))!.LockoutEnd > DateTimeOffset.UtcNow, "Five failed sign-ins lock the account");
    var mailHtml = VeloraMail.VerificationHtml("<script>test</script>", "654321", DateTime.UtcNow.AddMinutes(10), "https://example.test/verify");
    Check(mailHtml.Contains("max-width:600px") && mailHtml.Contains("<table") && !mailHtml.Contains("<script>") && mailHtml.Contains("654321"), "Email template is table-based and HTML-encodes dynamic values");
    foreach (var code in new[] { 401, 403, 404, 500 }) Check((int)(await Get(anonymous, "/ErrorPages/Page" + code)).Response.StatusCode == code, "Branded error route returns HTTP " + code);
    var logout = await Post(alice, "/Message/Inbox", "/Login/SignOutAccount", new());
    Check(logout.StatusCode == HttpStatusCode.Redirect && (await Get(alice, "/Message/Inbox")).Response.StatusCode == HttpStatusCode.Redirect, "POST logout removes authenticated access");
    await File.WriteAllTextAsync(fixturePath, JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true }));
    await File.WriteAllTextAsync(Path.Combine(root, "docs", "velora", "verification-results.json"), JsonSerializer.Serialize(new { passed = results.Count, checks = results, database = "isolated test database; no real SMTP delivery", checkedAtUtc = DateTime.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("All " + results.Count + " integration checks passed. No email was sent to a real recipient.");
}
catch (Exception exception)
{
    await File.WriteAllTextAsync(Path.Combine(root, "docs", "velora", mode == "register" ? "registration-results.json" : "verification-results.json"), JsonSerializer.Serialize(new { passed = results.Count, checks = results, failure = exception.Message }, new JsonSerializerOptions { WriteIndented = true }));
    Console.Error.WriteLine("FAIL " + exception.Message); Environment.ExitCode = 1;
}

public class Session
{
    public string Database { get; set; } = "";
    public string Password { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string? MessageId { get; set; }
    public Dictionary<string, string> Users { get; set; } = [];
}
