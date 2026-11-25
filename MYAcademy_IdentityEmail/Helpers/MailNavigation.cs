using Microsoft.EntityFrameworkCore;
using MYAcademy_IdentityEmail.Context;
using MYAcademy_IdentityEmail.Entities;
using MYAcademy_IdentityEmail.Models;

namespace MYAcademy_IdentityEmail.Helpers
{
    public static class MailNavigation
    {
        public static async Task<MailNavigationViewModel> LoadAsync(EmailContext context, AppUser user)
        {
            var personalStates = context.MessageUserStates.Where(x => x.UserId == user.Id);
            var hidden = personalStates.Where(x => x.IsDeleted || x.IsPermanentlyDeleted).Select(x => x.MessageId);
            var messages = context.Messages.Where(x => (x.ReceiverEmail == user.Email || x.SenderEmail == user.Email) && !hidden.Contains(x.MessageId));
            return new MailNavigationViewModel
            {
                User = user,
                Inbox = await messages.CountAsync(x => x.ReceiverEmail == user.Email),
                Unread = await messages.CountAsync(x => x.ReceiverEmail == user.Email && !x.IsRead),
                Sent = await messages.CountAsync(x => x.SenderEmail == user.Email),
                Starred = await messages.CountAsync(x => personalStates.Any(s => s.MessageId == x.MessageId && s.IsStarred)),
                Trash = await context.Messages.CountAsync(x => (x.ReceiverEmail == user.Email || x.SenderEmail == user.Email)
                    && personalStates.Any(s => s.MessageId == x.MessageId && s.IsDeleted && !s.IsPermanentlyDeleted)),
                Drafts = await context.DraftMessages.CountAsync(x => x.UserId == user.Id),
                Categories = await context.Categories.AsNoTracking().OrderBy(x => x.CategoryId).ToListAsync(),
                CategoryCounts = await personalStates.Where(x => x.CategoryId != null && !x.IsDeleted && !x.IsPermanentlyDeleted
                    && context.Messages.Any(m => m.MessageId == x.MessageId && (m.ReceiverEmail == user.Email || m.SenderEmail == user.Email)))
                    .GroupBy(x => x.CategoryId!.Value).ToDictionaryAsync(g => g.Key, g => g.Count())
            };
        }
    }
}
