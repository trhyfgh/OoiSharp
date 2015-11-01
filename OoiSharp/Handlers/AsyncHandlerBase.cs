using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace OoiSharp.Handlers
{
    public abstract class AsyncHandlerBase : HttpTaskAsyncHandler
    {
        public override bool IsReusable => true;
        
        public override Task ProcessRequestAsync(HttpContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            long signInTs;

            var ep = IPAddress.Parse(req.UserHostAddress);
            var viewer = Utils.Cookie.VerifyCookie(req.Cookies["viewer"]?.Value, ep);
            var world = Utils.Cookie.VerifyCookie(req.Cookies["world"]?.Value, ep);
            var token = Utils.Cookie.VerifyCookie(req.Cookies["token"]?.Value, out signInTs, ep);

            if(string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(world) || string.IsNullOrWhiteSpace(viewer)) {
                resp.StatusCode = 403;
                return Task.FromResult(0);
            }

            return ProcessRequestCoreAsync(ctx, viewer, world, signInTs);
        }

        protected abstract Task ProcessRequestCoreAsync(HttpContext ctx, string viewer, string world, long startTime);
    }
}