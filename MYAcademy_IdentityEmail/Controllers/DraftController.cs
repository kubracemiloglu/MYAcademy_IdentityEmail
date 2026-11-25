using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MYAcademy_IdentityEmail.Context;
using MYAcademy_IdentityEmail.Entities;
using MYAcademy_IdentityEmail.Models;

namespace MYAcademy_IdentityEmail.Controllers
{
    [Authorize]
    public class DraftController : Controller
    {
        private readonly EmailContext _context;
        private readonly UserManager<AppUser> _userManager;
        public DraftController(EmailContext context, UserManager<AppUser> userManager) { _context = context; _userManager = userManager; }
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            return View(await _context.DraftMessages.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.UpdatedAt).ToListAsync());
        }
        public async Task<IActionResult> Edit(string id)
        {
            var userId = _userManager.GetUserId(User);
            var draft = await _context.DraftMessages.AsNoTracking().SingleOrDefaultAsync(x => x.DraftMessageId == id && x.UserId == userId);
            if (draft == null) return NotFound();
            return View("~/Views/Message/CreateMessage.cshtml", new CreateMessageViewModel { DraftMessageId = id, ReceiverEmail = draft.ReceiverEmail, Subject = draft.Subject,
                MessageDetail = draft.MessageDetail, CategoryId = draft.CategoryId, Categories = await _context.Categories.OrderBy(x => x.CategoryId).ToListAsync() });
        }
        [HttpPost]
        public async Task<IActionResult> Save(CreateMessageViewModel model)
        {
            // Taslakta alıcı, konu ve içeriğin tamamlanmış olması zorunlu değildir.
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            if ((model.ReceiverEmail?.Length ?? 0) > 256 || (model.Subject?.Length ?? 0) > 200 || (model.MessageDetail?.Length ?? 0) > 20000) return BadRequest();
            if (model.CategoryId != null && !await _context.Categories.AnyAsync(x => x.CategoryId == model.CategoryId)) return BadRequest();
            var draft = model.DraftMessageId == null ? new DraftMessage { UserId = userId } : await _context.DraftMessages.SingleOrDefaultAsync(x => x.DraftMessageId == model.DraftMessageId && x.UserId == userId);
            if (draft == null) return NotFound();
            if (model.DraftMessageId == null) _context.DraftMessages.Add(draft);
            draft.ReceiverEmail = model.ReceiverEmail?.Trim() ?? ""; draft.Subject = model.Subject ?? ""; draft.MessageDetail = model.MessageDetail ?? "";
            draft.CategoryId = model.CategoryId; draft.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Taslağınız kaydedildi.";
            return RedirectToAction(nameof(Edit), new { id = draft.DraftMessageId });
        }
        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var userId = _userManager.GetUserId(User);
            var draft = await _context.DraftMessages.SingleOrDefaultAsync(x => x.DraftMessageId == id && x.UserId == userId);
            if (draft == null) return NotFound();
            _context.DraftMessages.Remove(draft); await _context.SaveChangesAsync();
            TempData["Success"] = "Taslak silindi."; return RedirectToAction(nameof(Index));
        }
    }
}
