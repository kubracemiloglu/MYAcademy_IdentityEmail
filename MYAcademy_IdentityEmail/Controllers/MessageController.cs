using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using MYAcademy_IdentityEmail.Models;
using MYAcademy_IdentityEmail.Context;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Controllers
{
    [Authorize]
    public class MessageController : Controller
    {
        private readonly EmailContext _context;
        private readonly UserManager<AppUser> _userManager;

        public MessageController(EmailContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Inbox(string? q, string filter = "all", string sort = "newest", int? categoryId = null, string? id = null)
        {
            var mailbox = await BuildMailbox("Inbox", q, filter, sort, categoryId, id);
            if (mailbox == null) return Challenge();
            ViewData["Mailbox"] = mailbox;
            var values = mailbox.Messages;
            return View(values);
        }
        public  async Task<IActionResult> Sendbox(string? q, string filter = "all", string sort = "newest", string? id = null)
        {
            var value = await _userManager.GetUserAsync(User);
            if (value?.Email == null) return Challenge();
            var mailbox = await BuildMailbox("Sendbox", q, filter, sort, null, id);
            ViewData["Mailbox"] = mailbox;
            var messageList = mailbox!.Messages;
            return View(messageList);
        }
        public async Task<IActionResult> CreateMessage(string? replyTo, string? forwardId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.Email == null) return Challenge();
            var model = new CreateMessageViewModel { Categories = await _context.Categories.OrderBy(x => x.CategoryId).ToListAsync() };
            var sourceId = replyTo ?? forwardId;
            if (sourceId != null)
            {
                var message = await AccessibleMessage(sourceId, user);
                if (message == null) return NotFound();
                model.Subject = (replyTo != null ? "Ynt: " : "İlt: ") + message.Subject;
                model.Subject = model.Subject[..Math.Min(model.Subject.Length, 200)];
                model.ReceiverEmail = replyTo != null ? (message.SenderEmail == user.Email ? message.ReceiverEmail : message.SenderEmail) : "";
                // Keep the complete body within the same limit as the original message.
                model.MessageDetail = message.MessageDetail;
                ViewData["SourceSender"] = message.SenderEmail;
                ViewData["SourceSubject"] = message.Subject;
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> CreateMessage(CreateMessageViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.Email == null) return Challenge();
            var receiver = await _userManager.FindByEmailAsync(model.ReceiverEmail?.Trim() ?? "");
            if (receiver == null) ModelState.AddModelError(nameof(model.ReceiverEmail), "Bu e-posta adresiyle kayıtlı bir kullanıcı bulunamadı.");
            if (model.CategoryId != null && !await _context.Categories.AnyAsync(x => x.CategoryId == model.CategoryId)) ModelState.AddModelError(nameof(model.CategoryId), "Geçerli bir kategori seçin.");
            model.Categories = await _context.Categories.OrderBy(x => x.CategoryId).ToListAsync();
            if (!ModelState.IsValid) return View(model);
            DraftMessage? draft = null;
            if (model.DraftMessageId != null)
            {
                draft = await _context.DraftMessages.SingleOrDefaultAsync(x => x.DraftMessageId == model.DraftMessageId && x.UserId == user.Id);
                if (draft == null) return NotFound();
            }
            var categoryId = model.CategoryId ?? await _context.Categories.Where(x => x.CategoryName == "Diğer").Select(x => (int?)x.CategoryId).FirstOrDefaultAsync();
            if (categoryId == null) { ModelState.AddModelError("", "Mesaj kategorileri henüz hazırlanmadı."); return View(model); }
            var message = new Message { MessageId = Guid.NewGuid().ToString(), ReceiverEmail = receiver!.Email!, SenderEmail = user.Email,
                Subject = model.Subject.Trim(), MessageDetail = model.MessageDetail, SentDate = DateTime.UtcNow, CategoryId = categoryId.Value, IsRead = false };
            _context.Messages.Add(message);
            if (model.CategoryId != null) _context.MessageUserStates.Add(new MessageUserState { UserId = user.Id, MessageId = message.MessageId, CategoryId = model.CategoryId });
            if (draft != null) _context.DraftMessages.Remove(draft);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Mesajınız gönderildi.";
            return RedirectToAction(nameof(Sendbox), new { id = message.MessageId });
        }

        public async Task<IActionResult> MessageDetail(string id, string folder = "Inbox")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.Email == null) return Challenge();
            var message = await AccessibleMessage(id, user, includeTrash: true);
            if (message == null) return NotFound();
            var state = await _context.MessageUserStates.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.Id && x.MessageId == id);
            if (state?.IsDeleted == true) folder = "Trash";
            else if (message.ReceiverEmail != user.Email) folder = "Sendbox";
            if (folder is not ("Inbox" or "Sendbox" or "Starred" or "Trash" or "Category")) folder = "Inbox";
            var mailbox = await BuildMailbox(folder, null, "all", "newest", state?.CategoryId, id);
            if (mailbox?.SelectedMessage == null) return NotFound();
            return View("Mailbox", mailbox);
        }

        public async Task<IActionResult> Starred(string? q, string filter = "all", string sort = "newest", string? id = null) => await Folder("Starred", q, filter, sort, null, id);
        public async Task<IActionResult> Trash(string? q, string filter = "all", string sort = "newest", string? id = null) => await Folder("Trash", q, filter, sort, null, id);
        public async Task<IActionResult> Category(int categoryId, string? q, string filter = "all", string sort = "newest", string? id = null) => await Folder("Category", q, filter, sort, categoryId, id);

        private async Task<IActionResult> Folder(string folder, string? q, string filter, string sort, int? categoryId, string? id)
        {
            var mailbox = await BuildMailbox(folder, q, filter, sort, categoryId, id);
            return mailbox == null ? Challenge() : View("Mailbox", mailbox);
        }

        [HttpPost]
        public async Task<IActionResult> SetRead(string id, bool isRead, string? returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.Email == null) return Challenge();
            var message = await AccessibleMessage(id, user, includeTrash: true);
            if (message == null || message.ReceiverEmail != user.Email) return NotFound();
            message.IsRead = isRead;
            await _context.SaveChangesAsync();
            if (!isRead) TempData["PreserveUnread"] = true;
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = true });
            return LocalReturn(returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStar(string id, string? returnUrl)
        {
            var state = await PersonalState(id);
            if (state == null) return NotFound();
            var updated = await _context.MessageUserStates
                .Where(x => x.MessageUserStateId == state.MessageUserStateId && !x.IsDeleted && !x.IsPermanentlyDeleted)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsStarred, x => !x.IsStarred));
            if (updated == 0) return NotFound();
            return LocalReturn(returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id, string? returnUrl)
        {
            var state = await PersonalState(id);
            if (state == null) return NotFound();
            state.IsDeleted = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Mesaj çöp kutunuza taşındı. Diğer katılımcının mesajı korundu.";
            return LocalReturn(returnUrl, nameof(Inbox));
        }

        [HttpPost]
        public async Task<IActionResult> Restore(string id)
        {
            var state = await PersonalState(id, true);
            if (state == null || !state.IsDeleted) return NotFound();
            state.IsDeleted = false;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Mesaj geri yüklendi.";
            return RedirectToAction(nameof(Trash));
        }

        [HttpPost]
        public async Task<IActionResult> PermanentlyDelete(string id)
        {
            var state = await PersonalState(id, true);
            if (state == null || !state.IsDeleted) return NotFound();
            state.IsPermanentlyDeleted = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Mesaj sizin hesabınızdan kalıcı olarak kaldırıldı.";
            return RedirectToAction(nameof(Trash));
        }

        [HttpPost]
        public async Task<IActionResult> SetCategory(string id, int? categoryId, string? returnUrl)
        {
            if (categoryId != null && !await _context.Categories.AnyAsync(x => x.CategoryId == categoryId)) return BadRequest();
            var state = await PersonalState(id);
            if (state == null) return NotFound();
            state.CategoryId = categoryId;
            await _context.SaveChangesAsync();
            return LocalReturn(returnUrl);
        }

        private IActionResult LocalReturn(string? url, string fallback = nameof(Inbox)) => url != null && Url.IsLocalUrl(url) ? LocalRedirect(url) : RedirectToAction(fallback);

        private async Task<Message?> AccessibleMessage(string id, AppUser user, bool includeTrash = false) => await _context.Messages
            .SingleOrDefaultAsync(x => x.MessageId == id && (x.ReceiverEmail == user.Email || x.SenderEmail == user.Email)
                && !_context.MessageUserStates.Any(s => s.UserId == user.Id && s.MessageId == id && (s.IsPermanentlyDeleted || (!includeTrash && s.IsDeleted))));

        private async Task<MessageUserState?> PersonalState(string id, bool includeTrash = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || await AccessibleMessage(id, user, includeTrash) == null) return null;
            var state = await _context.MessageUserStates.SingleOrDefaultAsync(x => x.MessageId == id && x.UserId == user.Id);
            if (state != null) return state;
            state = new MessageUserState { MessageId = id, UserId = user.Id };
            _context.MessageUserStates.Add(state);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Another request created the unique user/message row first.
                _context.Entry(state).State = EntityState.Detached;
                state = await _context.MessageUserStates.SingleAsync(x => x.MessageId == id && x.UserId == user.Id);
            }
            if (state.IsPermanentlyDeleted || (!includeTrash && state.IsDeleted)) return null;
            return state;
        }

        private async Task<MailboxViewModel?> BuildMailbox(string folder, string? q, string filter, string sort, int? categoryId, string? id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.Email == null) return null;
            var states = _context.MessageUserStates.Where(x => x.UserId == user.Id);
            var query = _context.Messages.AsNoTracking().Where(x => x.ReceiverEmail == user.Email || x.SenderEmail == user.Email);
            query = folder == "Trash"
                ? query.Where(x => states.Any(s => s.MessageId == x.MessageId && s.IsDeleted && !s.IsPermanentlyDeleted))
                : query.Where(x => !states.Any(s => s.MessageId == x.MessageId && (s.IsDeleted || s.IsPermanentlyDeleted)));
            if (folder == "Inbox") query = query.Where(x => x.ReceiverEmail == user.Email);
            if (folder == "Sendbox") query = query.Where(x => x.SenderEmail == user.Email);
            if (folder == "Starred") query = query.Where(x => states.Any(s => s.MessageId == x.MessageId && s.IsStarred));
            if (folder == "Category") query = query.Where(x => states.Any(s => s.MessageId == x.MessageId && s.CategoryId == categoryId));
            var model = new MailboxViewModel { Folder = folder, Title = folder switch { "Sendbox" => "Gönderilenler", "Starred" => "Yıldızlılar", "Trash" => "Çöp Kutusu", "Category" => "Kategori", _ => "Gelen Kutusu" },
                Search = q, Filter = filter, Sort = sort, CategoryId = categoryId, TotalCount = await query.CountAsync(), UnreadCount = await query.CountAsync(x => x.ReceiverEmail == user.Email && !x.IsRead),
                Categories = await _context.Categories.AsNoTracking().OrderBy(x => x.CategoryId).ToListAsync() };
            if (folder == "Category") model.Title = model.Categories.FirstOrDefault(x => x.CategoryId == categoryId)?.CategoryName ?? "Kategori";
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                var people = _context.Users.Where(x => (x.Name + " " + x.Surname).Contains(q)).Select(x => x.Email);
                query = query.Where(x => x.Subject.Contains(q) || x.MessageDetail.Contains(q) || x.SenderEmail.Contains(q) || x.ReceiverEmail.Contains(q) || people.Contains(x.SenderEmail) || people.Contains(x.ReceiverEmail));
            }
            if (filter == "unread") query = query.Where(x => x.ReceiverEmail == user.Email && !x.IsRead);
            if (filter == "starred") query = query.Where(x => states.Any(s => s.MessageId == x.MessageId && s.IsStarred));
            model.Messages = await (sort == "oldest" ? query.OrderBy(x => x.SentDate) : query.OrderByDescending(x => x.SentDate)).ToListAsync();
            model.States = await states.AsNoTracking().ToDictionaryAsync(x => x.MessageId);
            var emails = model.Messages.SelectMany(x => new[] { x.SenderEmail, x.ReceiverEmail }).Distinct().ToList();
            var peopleList = await _context.Users.AsNoTracking().Where(x => emails.Contains(x.Email!)).ToListAsync();
            model.People = peopleList.Where(x => x.Email != null).GroupBy(x => x.Email!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            model.SelectedMessage = TempData["PreserveUnread"] is true ? null
                : id == null ? model.Messages.FirstOrDefault() : model.Messages.FirstOrDefault(x => x.MessageId == id);
            return model;
        }

    }
}
