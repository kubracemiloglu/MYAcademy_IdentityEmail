using Microsoft.AspNetCore.Mvc;

namespace MYAcademy_IdentityEmail.Controllers
{
    public class ErrorPagesController : Controller
    {
        public IActionResult Page401()
        {
            Response.StatusCode = 401;
            return View();
        }

        public IActionResult Page403()
        {
            Response.StatusCode = 403;
            return View();
        }

        public IActionResult Page404()
        {
            Response.StatusCode = 404;
            ViewData["VeloraError"] = true;
            return View();
        }

        public IActionResult Page500()
        {
            Response.StatusCode = 500;
            return View();
        }

        public IActionResult Status(int statusCode)
        {
            Response.StatusCode = statusCode is >= 400 and <= 599 ? statusCode : 404;
            ViewData["VeloraError"] = true;
            return View("Page" + (Response.StatusCode is 401 or 403 or 500 ? Response.StatusCode : 404));
        }
    }
}
