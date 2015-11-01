using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace OoiSharp.Handlers
{
    public class KcsWorldImgXLateHandler : AsyncHandlerBase
    {
        protected override async Task ProcessRequestCoreAsync(HttpContext ctx, string viewer, string world, long startTime)
        {
            var req = ctx.Request;
            var resp = ctx.Response;
            
            var url = world + req.Url.PathAndQuery.Substring(1);
            var encodedHost = req.Url.Host.Replace('.', '_');
            var encodedWorld = string.Join("_", new Uri(world).Host.Split('.').Select(x => x.PadLeft(3, '0')));
            HttpWebRequest hwr = WebRequest.CreateHttp(url.Replace(encodedHost, encodedWorld));
            hwr.Method = req.HttpMethod;
            hwr.Timeout = 15000;
            hwr.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            hwr.UserAgent = ConfigurationManager.AppSettings["ua"];
            hwr.AllowAutoRedirect = true;
            hwr.KeepAlive = true;

            HttpWebResponse proxyResponse;
            try {
                proxyResponse = (HttpWebResponse)await hwr.GetResponseAsync();
            } catch(WebException e) {
                proxyResponse = (HttpWebResponse)e.Response;
            }
            if(proxyResponse == null) {
                resp.StatusCode = 502;
                return;
            }
            using(proxyResponse) {
                resp.StatusCode = (int)proxyResponse.StatusCode;
                resp.ContentType = proxyResponse.ContentType;
                resp.Cache.SetCacheability(HttpCacheability.NoCache);

                using(var proxyStream = proxyResponse.GetResponseStream()) {
                    await proxyStream.CopyToAsync(resp.OutputStream);
                }
            }
        }
    }
}
