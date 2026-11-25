using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MYAcademy_IdentityEmail.Context;
using MYAcademy_IdentityEmail.Entities;
using MYAcademy_IdentityEmail.Helpers;

namespace MYAcademy_IdentityEmail.ViewComponents
{
    public class VeloraNavigationViewComponent : ViewComponent
    {
        private readonly EmailContext _context;
        private readonly UserManager<AppUser> _userManager;
        public VeloraNavigationViewComponent(EmailContext context, UserManager<AppUser> userManager) { _context = context; _userManager = userManager; }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            return user == null ? Content("") : View(await MailNavigation.LoadAsync(_context, user));
        }
    }
}
