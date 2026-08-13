using Microsoft.AspNetCore.Mvc;
using APCVehicleTracker.Models;

namespace APCVehicleTracker.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // dummy data to show login
            if (model.Username == "admin" && model.Password == "password")
            {
                HttpContext.Session.SetString("User", model.Username);

                
                return RedirectToAction("Index", "Vehicle");
            }

            ViewBag.Error = "Invalid username or password.";
            return View(model);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
