using Owin;
using System;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using OoiSharp.Models;
using System.Net;

namespace OoiSharp
{
    public partial class Startup
    {
        public void ConfigureAuth(IAppBuilder app)
        {
            app.CreatePerOwinContext(DbContext.Create);
            app.CreatePerOwinContext<UserManager>(UserManager.Create);
            app.CreatePerOwinContext<SignInManager>(SignInManager.Create);

            app.UseCookieAuthentication(new CookieAuthenticationOptions() {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/Auth/Login"),
                LogoutPath = new PathString("/Auth/Logout"),
                Provider = new CookieAuthenticationProvider() {
                    OnValidateIdentity = async x => {
                        long signInTs;
                        var username = Utils.Cookie.VerifyCookie(x.Request.Cookies["viewer"], out signInTs, IPAddress.Parse(x.Request.RemoteIpAddress));
                        if(x.Identity.GetUserName() == username) {
                            var user = await x.OwinContext.GetUserManager<UserManager>().FindByNameAsync(username);
                            if(user?.SignInTimestamp == signInTs) {
                                return;
                            }
                        }
                        x.RejectIdentity();
                        x.Response.Cookies.Delete("viewer");
                    }
                }
            });
        }
    }
}
