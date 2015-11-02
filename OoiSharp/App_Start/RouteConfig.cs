using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace OoiSharp
{
    internal class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("kcs/{*catchall}");
            routes.IgnoreRoute("kcsapi/{*catchall}");
            routes.RouteExistingFiles = false;

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}",
                defaults: new { controller = "Game", action = "Index" }
            );
        }
    }
}
