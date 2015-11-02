using System;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.IO;
using Newtonsoft.Json;

namespace OoiSharp.Handlers
{
    public class KcsApiStart2Handler : AsyncHandlerBase
    {
        private const int Start2ValidityPeriod = 8 * 60 * 1000;

        public static bool IsCachedStart2Avaliable => start2Content != null;
        private static string start2Content;
        private static long start2Timestamp;

        internal static void LoadDiskCache(HttpServerUtility server)
        {
            var cache = server.MapPath("~/App_Data/api_start2.json");
            if(!File.Exists(cache)) return;

            start2Content = File.ReadAllText(cache);
            start2Timestamp = (long)((File.GetLastWriteTimeUtc(cache) - Utils.UnixTimestamp.Epoch.UtcDateTime).TotalMilliseconds);
        }

        protected override async Task ProcessRequestCoreAsync(HttpContext ctx, string viewer, string world, long startTime)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            if(!req.IsAuthenticated) {
                resp.StatusCode = 403;
                return;
            }
            
            var currentTs = Utils.UnixTimestamp.CurrentMillisecondTimestamp;
            var start2Ts = start2Timestamp;
            if((currentTs - startTime < Start2ValidityPeriod) && (currentTs - start2Ts > Start2ValidityPeriod)) {
                HttpWebRequest hwr = WebRequest.CreateHttp(world + "kcsapi/api_start2");
                if(req.Headers["Referer"] != null) {
                    var hdrLine = req.Headers["Referer"];
                    var schema = hdrLine.IndexOf("//") + 2;
                    var kcsUrl = hdrLine.IndexOf("/kcs", schema);
                    if(kcsUrl > 0) {
                        hwr.Referer = world + hdrLine.Substring(kcsUrl + 1);
                    }
                }

                var proxyResponse = await Utils.Misc.ForwardRequest(req, hwr);
                if(proxyResponse != null) {
                    using(proxyResponse) {
                        if(proxyResponse.StatusCode == HttpStatusCode.OK) {
                            string newStart2;
                            using(StreamReader rdr = new StreamReader(proxyResponse.GetResponseStream())) {
                                newStart2 = (await rdr.ReadToEndAsync());
                            }
                            JsonReader json = new JsonTextReader(new StringReader(newStart2.Substring(7)));
                            while(json.Read()) {
                                if(json.TokenType == JsonToken.PropertyName && json.Value.ToString() == "api_result") {
                                    if(json.ReadAsInt32() == 1) {
                                        if(System.Threading.Interlocked.CompareExchange(ref start2Timestamp, currentTs, start2Ts) == start2Ts) {
                                            start2Content = newStart2;
                                            File.WriteAllText(ctx.Server.MapPath("~/App_Data/api_start2.json"), start2Content);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if(start2Content == null) {
                ctx.Response.StatusCode = 503;
                return;
            }

            ctx.Response.ContentType = "text/plain";
            ctx.Response.Write(start2Content);
            return;
        }
    }
}
