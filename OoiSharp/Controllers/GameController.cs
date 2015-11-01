using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using OoiSharp.Models;
using Microsoft.Owin.Security;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.AspNet.Identity;

namespace OoiSharp.Controllers
{
    [Authorize]
    public class GameController : Controller
    {
        [AllowAnonymous]
        public ActionResult Index()
        {
            if(!Request.IsAuthenticated) {
                return RedirectToAction("Login", "Auth");
            }
            
            return View(new ViewModelBase());
        }

        public ActionResult Frame()
        {
            var ep = System.Net.IPAddress.Parse(Request.UserHostAddress);
            var world = Utils.Cookie.VerifyCookie(Request.Cookies["world"]?.Value, ep);
            var token = Utils.Cookie.VerifyCookie(Request.Cookies["token"]?.Value, ep);
            var startTime = Utils.Cookie.VerifyCookie(Request.Cookies["startTime"]?.Value, ep);

            if(string.IsNullOrWhiteSpace(startTime) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(world)) {
                ViewBag.Message = Request.Cookies["message"]?.Value;
                return View("Error");
            }

            var sitePrefix = Request.Url.Scheme + "://" + Request.Url.Host;
            if(!Request.Url.IsDefaultPort) sitePrefix += ":" + Request.Url.Port;
            ViewBag.SitePrefix = sitePrefix;
            ViewBag.ApiToken = token;
            ViewBag.StartTime = startTime;

            return View();
        }
    }
}
