using System;
using System.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;

namespace OoiSharp.Handlers
{
    public class KcsForwardHandler : AsyncHandlerBase
    {
        protected override async Task ProcessRequestCoreAsync(HttpContext ctx, string viewer, string world, long startTime)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            if(req.ContentLength > 4096) {
                resp.StatusCode = 413;
                return;
            }

            HttpWebRequest hwr = WebRequest.CreateHttp(world + req.Url.PathAndQuery.Substring(1));
            if(req.Headers["Referer"] != null) {
                var hdrLine = req.Headers["Referer"];
                var schema = hdrLine.IndexOf("//") + 2;
                var kcsUrl = hdrLine.IndexOf("/kcs", schema);
                if(kcsUrl > 0) {
                    hwr.Referer = world + hdrLine.Substring(kcsUrl + 1);
                }
            }

            var proxyResponse = await Utils.Misc.ForwardRequest(req, hwr);
            if(proxyResponse == null) {
                resp.StatusCode = 502;
                return;
            }
            using(proxyResponse) {
                resp.StatusCode = (int)proxyResponse.StatusCode;
                foreach(var hdr in proxyResponse.Headers.AllKeys) {
                    switch(hdr) {
                        case "Server":
                        case "Connection":
                        case "Accept-Ranges":
                        case "Content-Length":
                        case "Transfer-Encoding":
                        case "WWW-Authenticate":
                            break;
                        case "Content-Type":
                            resp.ContentType = proxyResponse.ContentType;
                            break;
                        case "Cache-Control":
                            {
                                var cacheControl = CacheControlHeaderValue.Parse(proxyResponse.Headers[hdr]);
                                if(cacheControl.Public) {
                                    resp.Cache.SetCacheability(HttpCacheability.Public);
                                }
                                if(cacheControl.NoCache) {
                                    resp.Cache.SetCacheability(HttpCacheability.NoCache);
                                }
                                if(cacheControl.MaxAge != null) {
                                    resp.Cache.SetMaxAge(cacheControl.MaxAge.Value);
                                }
                            }
                            break;
                        default:
                            if(WebHeaderCollection.IsRestricted(hdr, true)) {
                                ctx.Response.StatusCode = 501;
                                return;
                            }
                            resp.Headers[hdr] = proxyResponse.Headers[hdr];
                            break;
                    }
                }
                if(proxyResponse.ContentLength != 0) {
                    using(var proxyStream = proxyResponse.GetResponseStream()) {
                        await proxyStream.CopyToAsync(resp.OutputStream);
                    }
                }
            }
        }
    }
}
