using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MYAcademy_IdentityEmail.Context;
using MYAcademy_IdentityEmail.Entities;
using MYAcademy_IdentityEmail.Helpers;
using MYAcademy_IdentityEmail.Models;

namespace MYAcademy_IdentityEmail.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly EmailContext _context;
        private readonly UserManager<AppUser> _userManager;
        public DashboardController(EmailContext context, UserManager<AppUser> userManager) { _context = context; _userManager = userManager; }
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.Email == null) return Challenge();
            var counts = await MailNavigation.LoadAsync(_context, user);
            var startLocal = DateTime.Today.AddDays(-6);
            var startUtc = startLocal.ToUniversalTime();
            var endUtc = DateTime.Today.AddDays(1).ToUniversalTime();
            var visible = _context.Messages.AsNoTracking().Where(x => (x.ReceiverEmail == user.Email || x.SenderEmail == user.Email)
                && !_context.MessageUserStates.Any(s => s.UserId == user.Id && s.MessageId == x.MessageId && (s.IsDeleted || s.IsPermanentlyDeleted)));
            var recent = await visible.Where(x => x.SentDate >= startUtc && x.SentDate < endUtc).Select(x => new { x.SentDate, x.ReceiverEmail, x.SenderEmail }).ToListAsync();
            var model = new DashboardViewModel { Counts = counts, RecentMessages = await visible.OrderByDescending(x => x.SentDate).Take(5).ToListAsync() };
            for (var day = 0; day < 7; day++)
            {
                var date = startLocal.AddDays(day);
                model.Labels.Add(date.ToString("d MMM", VeloraDisplay.Turkish));
                model.Received.Add(recent.Count(x => DateTime.SpecifyKind(x.SentDate, DateTimeKind.Utc).ToLocalTime().Date == date && x.ReceiverEmail == user.Email));
                model.Sent.Add(recent.Count(x => DateTime.SpecifyKind(x.SentDate, DateTimeKind.Utc).ToLocalTime().Date == date && x.SenderEmail == user.Email));
            }
            return View(model);
        }
    }
}
