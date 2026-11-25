using Microsoft.AspNetCore.Mvc;

namespace MYAcademy_IdentityEmail.ViewComponents.MessageLayoutViewComponents
{
    [ViewComponent(Name = "_MessageLayoutStyleComponentPartial")]
    public class _MessageLayoutStyleComponentPartial: ViewComponent
    {
        
        public IViewComponentResult Invoke()
        {
            return View();
        }
    }
}
