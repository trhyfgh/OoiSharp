using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using OoiSharp.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace OoiSharp.Utils
{
    public class Misc
    {
        public static void CheckConfig(ViewModelBase mdl)
        {
            if(Cookie.IsHmacKeyTemporary) {
                mdl.Warn.Add("验证密钥未正确配置，已启用临时密钥。可能无法保持登陆状态。");
            }
            var maintenance = KcAuth.MaintenanceMessage;
            if(maintenance != null) {
                mdl.Info.Add(maintenance);
            }
            if(!Handlers.KcsApiStart2Handler.IsCachedStart2Avaliable) {
                mdl.Info.Add("当前api_start2的缓存副本不可用。于10分钟以前登陆的用户可能无法进入游戏。");
            }
        }

        public static async Task<HttpWebResponse> ForwardRequest(HttpRequest req, HttpWebRequest hwr)
        {
            hwr.Method = req.HttpMethod;
            hwr.Timeout = 15000;

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

            hwr.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            hwr.UserAgent = ConfigurationManager.AppSettings["ua"];
            hwr.AllowAutoRedirect = true;
            hwr.KeepAlive = true;

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
    }
}