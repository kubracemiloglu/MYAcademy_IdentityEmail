using Microsoft.AspNetCore.Mvc;

namespace MYAcademy_IdentityEmail.ViewComponents.MessageLayoutViewComponents
{
    [ViewComponent(Name = "_MessageLayoutScriptComponentPartial")]
    public class _MessageLayoutScriptComponentPartial:ViewComponent
    {
   
        public IViewComponentResult Invoke()
        {
            return View();
        }
    }
}
