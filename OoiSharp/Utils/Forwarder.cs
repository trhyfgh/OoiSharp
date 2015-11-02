using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace OoiSharp.Utils
{
    internal static class Forwarder
    {
        private static string dmmIp;
        private static WebProxy proxy;
        private static string userAgent;
        private static int requestTimeout;
        private static bool proxyDmmOnly;

        public static bool IsConfigurationValid => userAgent != null && (proxy == null || dmmIp == null);

        public static void ConfigForwarder()
        {
            if(IsConfigurationValid) throw new InvalidOperationException();

            IPAddress junk;
            var cfgDmmIp = ConfigurationManager.AppSettings["dmmIp"];
            if(!string.IsNullOrWhiteSpace(cfgDmmIp) && IPAddress.TryParse(cfgDmmIp, out junk)) {
                dmmIp = cfgDmmIp;
            }

            var cfgProxy = ConfigurationManager.AppSettings["proxy"];
            if(!string.IsNullOrWhiteSpace(cfgProxy) && Uri.IsWellFormedUriString(cfgProxy, UriKind.Absolute)) {
                proxy = new WebProxy(cfgProxy);
            }

            var cfgProxyDmmOnly = ConfigurationManager.AppSettings["proxyDmmOnly"];
            if(string.IsNullOrWhiteSpace(cfgProxyDmmOnly) || !bool.TryParse(cfgProxyDmmOnly, out proxyDmmOnly)) {
                proxyDmmOnly = true;
            }

            var cfgRequestTimeout = ConfigurationManager.AppSettings["requestTimeout"];
            if(string.IsNullOrWhiteSpace(cfgRequestTimeout) || !int.TryParse(cfgRequestTimeout, out requestTimeout)) {
                requestTimeout = 15000;
            }

            var cfgUserAgent = ConfigurationManager.AppSettings["userAgent"];
            if(string.IsNullOrWhiteSpace(cfgUserAgent)) {
                userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/42.0.2311.135 Safari/537.36 Edge/12.10240";
            } else {
                userAgent = cfgUserAgent;
            }
        }

        public static async Task<HttpWebResponse> ForwardRequest(HttpRequest req, string uri, string referer = null)
        {
            var hwr = CreateRequest(uri);
            hwr.Method = req.HttpMethod;
            if(referer != null) {
                hwr.Referer = referer;
            }

            foreach(var hdr in req.Headers.AllKeys) {
                switch(hdr) {
                    case "Accept":
                        hwr.Accept = req.Headers[hdr];
                        break;
                    case "Content-Type":
                        hwr.ContentType = req.ContentType;
                        break;
                    case "Content-Length":
                    case "User-Agent": // Forging UA
                    case "Connection": // Keep alive anyway
                    case "Host":
                    case "Proxy-Connection":
                    case "Range":
                    case "Date":
                    case "Cookie":
                    case "Transfer-Encoding":
                    case "Accept-Encoding":
                    case "Referer":
                        break; //Ignore these.
                    case "Expect":
                        return null;
                    case "If-Modified-Since":
                        {
                            var hdrLine = req.Headers[hdr];
                            var semicolon = hdrLine.IndexOf(';');
                            if(semicolon < 0) semicolon = hdrLine.Length;
                            hdrLine = hdrLine.Substring(0, semicolon);
                            hwr.IfModifiedSince = DateTime.Parse(hdrLine);
                        }
                        break;
                    default:
                        if(WebHeaderCollection.IsRestricted(hdr, false)) {
                            return null;
                        }
                        hwr.Headers[hdr] = req.Headers[hdr];
                        break;
                }
            }

            if(req.HttpMethod != "GET" && req.HttpMethod != "HEAD" && req.ContentLength != 0) {
                req.InputStream.Position = 0;
                using(var outGoingStream = await hwr.GetRequestStreamAsync())
                    await req.InputStream.CopyToAsync(outGoingStream);
            }

            try {
                return (HttpWebResponse)await hwr.GetResponseAsync();
            } catch(WebException e) {
                return (HttpWebResponse)e.Response;
            }
        }

        public static HttpWebRequest CreateRequest(string url)
        {
            if(!IsConfigurationValid) throw new InvalidOperationException();

            bool isDmm = false;
            const string DmmMainDomain = "www.dmm.com";

            if(proxyDmmOnly || (dmmIp != null)) {
                var schemeEnd = url.IndexOf("://") + 3;
                var hostEnd = url.IndexOf('/', schemeEnd);
                if(hostEnd > schemeEnd) {
                    if(url.Substring(schemeEnd, hostEnd - schemeEnd) == DmmMainDomain) {
                        isDmm = true;
                        if(dmmIp != null) {
                            url = url.Substring(0, schemeEnd) + dmmIp + url.Substring(hostEnd);
                        }
                    }
                }
            }

            var req = WebRequest.CreateHttp(url);
            req.AllowAutoRedirect = true;
            req.KeepAlive = true;
            req.ReadWriteTimeout = req.Timeout = requestTimeout;
            req.UserAgent = userAgent;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            if(isDmm) {
                req.Host = DmmMainDomain;
                req.Proxy = proxy;
            } else if(!proxyDmmOnly) {
                req.Proxy = proxy;
            }

            return req;
        }
    }
}
