﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace OoiSharp
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            Utils.Cookie.ConfigKey();
            Utils.Forwarder.ConfigForwarder();
            Handlers.KcsApiStart2Handler.LoadDiskCache(Server);
        }
    }
}
