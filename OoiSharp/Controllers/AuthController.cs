using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using OoiSharp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace OoiSharp.Controllers
{
    public class AuthController : Controller
    {
        public SignInManager SignInManager => HttpContext.GetOwinContext().Get<SignInManager>();
        public UserManager UserManager => HttpContext.GetOwinContext().GetUserManager<UserManager>();
        private IAuthenticationManager AuthenticationManager => HttpContext.GetOwinContext().Authentication;
        
        public async Task<ActionResult> Login()
        {
            if(!Request.IsAuthenticated) {
                long signInTs;
                var username = Utils.Cookie.VerifyCookie(Request.Cookies["viewer"]?.Value, out signInTs, System.Net.IPAddress.Parse(Request.UserHostAddress));

                if(!string.IsNullOrWhiteSpace(username)) {
                    if(await SignInAsync(username, signInTs, false)) {
                        return RedirectToAction("Index", "Game");
                    } else {
                        SignOut();
                    }
                }
            }

            var mdl = new LoginViewModel();
            return View(mdl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel mdl)
        {
            try {
                var ts = Utils.UnixTimestamp.CurrentMillisecondTimestamp;
                var cookieExpire = DateTime.UtcNow.AddMilliseconds(mdl.SessionTime);
                var auth = await Utils.KcAuth.FetchAuthParamAsync(mdl.Username, mdl.Password);
                var ep = System.Net.IPAddress.Parse(Request.UserHostAddress);

                if(!await SignInAsync(auth.Item1, ts, true)) {
                    throw new Utils.KcAuth.KcAuthException("ASP.NET Identity登陆失败");
                }

                var viewer = Utils.Cookie.SignCookie(auth.Item1, ts, mdl.IpBound ? ep : null, mdl.SessionTime);
                Response.SetCookie(new HttpCookie("viewer", viewer) { HttpOnly = true, Expires = cookieExpire });

                if(auth.Item5 == null) {
                    var world = Utils.Cookie.SignCookie(auth.Item2, ts, mdl.IpBound ? ep : null, mdl.SessionTime);
                    var token = Utils.Cookie.SignCookie(auth.Item3, ts, mdl.IpBound ? ep : null, mdl.SessionTime);
                    var startTime = Utils.Cookie.SignCookie(auth.Item4, ts, mdl.IpBound ? ep : null, mdl.SessionTime);

                    Response.SetCookie(new HttpCookie("world", world) { HttpOnly = true, Expires = cookieExpire });
                    Response.SetCookie(new HttpCookie("token", token) { HttpOnly = true, Expires = cookieExpire });
                    Response.SetCookie(new HttpCookie("startTime", startTime) { HttpOnly = true, Expires = cookieExpire });
                } else {
                    Response.SetCookie(new HttpCookie("message", auth.Item5) { HttpOnly = true, Expires = cookieExpire });
                }

                return RedirectToAction("Index", "Game");
            } catch(Utils.KcAuth.KcAuthException e) {
                mdl.Error.Add(e.Message);
                return View(mdl);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            SignOut();
            return RedirectToAction("Login", "Auth");
        }

        private void SignOut()
        {
            AuthenticationManager.SignOut();
            Response.SetCookie(new HttpCookie("viewer", "deleted") { Expires = Utils.UnixTimestamp.Epoch.UtcDateTime });
            Response.SetCookie(new HttpCookie("world", "deleted") { Expires = Utils.UnixTimestamp.Epoch.UtcDateTime });
            Response.SetCookie(new HttpCookie("token", "deleted") { Expires = Utils.UnixTimestamp.Epoch.UtcDateTime });
            Response.SetCookie(new HttpCookie("startTime", "deleted") { Expires = Utils.UnixTimestamp.Epoch.UtcDateTime });
        }

        private async Task<bool> SignInAsync(string username, long timestamp, bool updateTs)
        {
            User user;

            if((user = await UserManager.FindByNameAsync(username)) == null) {
                if(!(await UserManager.CreateAsync(user = new User() { UserName = username, SignInTimestamp = timestamp })).Succeeded) {
                    return false;
                }
                updateTs = false;
            }

            if(updateTs) {
                user.SignInTimestamp = timestamp;
                await HttpContext.GetOwinContext().Get<DbContext>().SaveChangesAsync();
            } else if(user.SignInTimestamp != timestamp) {
                return false;
            }

            AuthenticationManager.SignOut();
            var identity = await UserManager.CreateIdentityAsync(user, DefaultAuthenticationTypes.ApplicationCookie);
            AuthenticationManager.SignIn(new AuthenticationProperties() { IsPersistent = true }, identity);
            
            return true;
        }
    }
}
