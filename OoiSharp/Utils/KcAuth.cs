using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Text.RegularExpressions;
using System.Text;
using Newtonsoft.Json.Linq;
using Jint;

namespace OoiSharp.Utils
{
    public static class KcAuth
    {
        private const string PasswordResetUrl = "https://www.dmm.com/my/-/passwordreminder/";
        private const string LoginPageUrl = "https://www.dmm.com/my/-/login/";
        private const string AjaxTokenUrl = "https://www.dmm.com/my/-/login/ajax-get-token/";
        private const string AuthUrl = "https://www.dmm.com/my/-/login/auth/";
        private const string GameUrl = "http://www.dmm.com/netgame/social/-/gadgets/=/app_id=854854/";
        private const string OsapiUrlPrefix = " src=\"http://osapi.dmm.com/gadgets/ifr?";
        private const string KcsConstantsJs = "http://203.104.209.7/gadget/js/kcs_const.js";
        private const string DmmRequestUrl = "http://osapi.dmm.com/gadgets/makeRequest";
        private const int OsapiUrlOffset = 6;
        private const double KcInfoCacheHours = 0.25;

        private static readonly Regex Regex_dmmToken = new Regex("setRequestHeader.+['\"]DMM_TOKEN['\"][^'\"]+['\"]([^'\"]+)", RegexOptions.ECMAScript | RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex Regex_loginToken = new Regex("['\"]token['\"][^'\"]+['\"]([^'\"]+)", RegexOptions.ECMAScript | RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex Regex_email = new Regex("^[0-9a-z][0-9a-z+\\-._]*@[0-9a-z][0-9a-z\\-]*(\\.[0-9a-z][0-9a-z\\-]*)+[a-z]$", RegexOptions.ECMAScript | RegexOptions.Compiled);

        public static async Task<Tuple<string, string, string, string, string>> FetchAuthParamAsync(string username, string password)
        {
            if(username == null) throw new KcAuthException("用户名缺失");
            if(password == null) throw new KcAuthException("未输入密码");
            if(username.Length > 127) throw new KcAuthException("用户名太长");
            if(password.Length > 127) throw new KcAuthException("密码太长");
            if(!Regex_email.IsMatch(username)) throw new KcAuthException("用户名无效");

            var cookies = new CookieContainer();

            //Download login page
            var body = await GetPage(LoginPageUrl, cookies);
            var idx = body.IndexOf(AjaxTokenUrl);
            if(idx < 0) throw new KcAuthException("未能解析DMM登陆页面");

            //Locate 2 tokens required for AJAX request
            var match = Regex_dmmToken.Match(body, idx + AjaxTokenUrl.Length);
            if(!match.Success) throw new KcAuthException("未能定位DMM Token");
            System.Diagnostics.Debug.Assert(match.Groups.Count > 1);
            var dmmToken = match.Groups[1].Value;

            match = Regex_loginToken.Match(body, match.Index + match.Length);
            if(!match.Success) throw new KcAuthException("未能定位登陆token");
            System.Diagnostics.Debug.Assert(match.Groups.Count > 1);
            var loginToken = match.Groups[1].Value;

            //Download JSON data and parse out the token and field name
            body = await PostPage(AjaxTokenUrl, cookies, new Dictionary<string, string>() {["token"] = loginToken }, LoginPageUrl, new Dictionary<string, string>() {["DMM_TOKEN"] = dmmToken,["X-Requested-With"] = "XMLHttpRequest" });
            dynamic json = JObject.Parse(body);
            if(json.token == null || json.login_id == null || json.password == null) throw new KcAuthException("未能解析登陆token");

            loginToken = (string)json.token;
            string passwordField = (string)json.password;
            string usernameField = (string)json.login_id;

            //Perform login
            body = await PostPage(AuthUrl, cookies, new Dictionary<string, string>() {
                ["token"] = loginToken,
                [passwordField] = password,
                [usernameField] = username,
                ["login_id"] = username,
                ["password"] = password,
                ["client_id"] = "",
                ["display"] = "",
                ["prompt"] = "",
                ["path"] = "",
                ["save_login_id"] = "0",
                ["save_password"] = "0",
                ["use_auto_login"] = "0"
            }, LoginPageUrl);
            if(body.IndexOf(AjaxTokenUrl) > 0) throw new KcAuthException("用户名或密码错误");
            if(body.IndexOf(PasswordResetUrl) > 0) throw new KcAuthException("DMM拒绝登陆，需要修改密码");

            //Locate the game frame
            body = await GetPage(GameUrl, cookies);
            idx = body.IndexOf(OsapiUrlPrefix);
            if(idx < 0) throw new KcAuthException("未能定位游戏框架");
            var osapiUrl = body.Substring(idx + OsapiUrlOffset, body.IndexOf('"', idx + OsapiUrlOffset) - idx - OsapiUrlOffset);
            var query = HttpUtility.ParseQueryString(new Uri(osapiUrl).Query);

            await UpdateKcInfo(KcInfoCacheHours);

            string kcWorldUrl, kcLoginUrl, prefix;
            infoLock.EnterReadLock();
            try {
                if(lastUpdate < maintenanceStart) {
                    if(DateTimeOffset.UtcNow > maintenanceStart) {
                        return new Tuple<string, string, string, string, string>(query["viewer"], null, null, null, "维护中，预计将于" + maintenanceEnd.ToString() + "结束");
                    }
                }
                if(maintenanceOngoing) {
                    if(DateTimeOffset.UtcNow > maintenanceEnd) {
                        return new Tuple<string, string, string, string, string>(query["viewer"], null, null, null, "维护中，请于15分钟后再试");
                    }
                    return new Tuple<string, string, string, string, string>(query["viewer"], null, null, null, "维护中，预计将于" + maintenanceEnd.ToString() + "结束");
                }
                kcWorldUrl = KcAuth.kcWorldUrl;
            } finally {
                infoLock.ExitReadLock();
            }

            //Load world id
            body = await PostPage(DmmRequestUrl, cookies, new Dictionary<string, string>() {
                ["url"] = kcWorldUrl + query["viewer"] + "/1/" + UnixTimestamp.CurrentMillisecondTimestamp,
                ["httpMethod"] = "GET",
                ["headers"] = "",
                ["postData"] = "",
                ["authz"] = "",
                ["st"] = "",
                ["contentType"] = "JSON",
                ["numEntries"] = "3",
                ["getSummaries"] = "false",
                ["signOwner"] = "true",
                ["signViewer"] = "true",
                ["gadget"] = query["url"],
                ["container"] = "dmm",
                ["bypassSpecCache"] = "",
                ["getFullHeaders"] = "false",
                ["oauthState"] = ""
            }, osapiUrl);
            json = JObject.Parse(body.Substring(body.IndexOf('{'))).Properties().FirstOrDefault()?.Value;
            if((int?)json?.rc != 200 || json?.body == null) throw new KcAuthException("未能获取所在服务器，RPC请求失败");
            json = JObject.Parse(((string)json.body).Substring(7));
            if((int?)json.api_result != 1 || json.api_data?.api_world_id == null) throw new KcAuthException("未能获取所在服务器，RPC服务器错误");
            string world = "World_" + (int)json.api_data.api_world_id;

            //Perform authentication
            infoLock.EnterReadLock();
            try {
                if(!servers.ContainsKey(world)) throw new KcAuthException("服务器ID无效");
                prefix = servers[world];
                kcLoginUrl = KcAuth.kcLoginUrl;
            } finally {
                infoLock.ExitReadLock();
            }
            body = await PostPage(DmmRequestUrl, cookies, new Dictionary<string, string>() {
                ["url"] = prefix + kcLoginUrl + query["viewer"] + "/1/" + UnixTimestamp.CurrentMillisecondTimestamp,
                ["httpMethod"] = "GET",
                ["headers"] = "",
                ["postData"] = "",
                ["authz"] = "signed",
                ["st"] = query["st"],
                ["contentType"] = "JSON",
                ["numEntries"] = "3",
                ["getSummaries"] = "false",
                ["signOwner"] = "true",
                ["signViewer"] = "true",
                ["gadget"] = query["url"],
                ["container"] = "dmm",
                ["bypassSpecCache"] = "",
                ["getFullHeaders"] = "false",
                ["oauthState"] = ""
            }, osapiUrl);
            json = JObject.Parse(body.Substring(body.IndexOf('{'))).Properties().FirstOrDefault()?.Value;
            if((int?)json?.rc != 200 || json?.body == null) throw new KcAuthException("未能获取游戏认证token，RPC请求失败");
            json = JObject.Parse(((string)json.body).Substring(7));
            if((int?)json.api_result != 1 || json.api_token == null) throw new KcAuthException("未能获取游戏认证token，RPC服务器错误");

            return new Tuple<string, string, string, string, string>(query["viewer"], prefix, (string)json.api_token, (string)json.api_starttime ?? UnixTimestamp.CurrentMillisecondTimestamp.ToString(), null);
        }

        private static async Task<string> PostPage(string uri, CookieContainer cookies, Dictionary<string, string> postData, string refer = null, Dictionary<string, string> extraHeaders = null)
        {
            System.Diagnostics.Debug.Assert(postData.Count != 0);
            try {
                var req = Forwarder.CreateRequest(uri);
                req.CookieContainer = cookies;
                req.Referer = refer;

                if(extraHeaders != null) {
                    foreach(var kv in extraHeaders) {
                        req.Headers.Add(kv.Key, kv.Value);
                    }
                }

                StringBuilder sb = new StringBuilder();
                foreach(var kv in postData) {
                    sb.Append(kv.Key);
                    sb.Append('=');
                    sb.Append(HttpUtility.UrlEncode(kv.Value));
                    sb.Append('&');
                }

                var binData = Encoding.ASCII.GetBytes(sb.ToString(0, sb.Length - 1));
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";
                req.ContentLength = binData.Length;

                using(var rStr = await req.GetRequestStreamAsync()) {
                    await rStr.WriteAsync(binData, 0, binData.Length);
                }

                using(var response = (HttpWebResponse)await req.GetResponseAsync())
                using(var reader = new StreamReader(response.GetResponseStream())) {
                    return await reader.ReadToEndAsync();
                }
            } catch(IOException e) {
                throw new KcAuthException("请求以下资源时发生网络错误 " + e.Message + " " + uri, e);
            } catch(WebException e) {
                throw new KcAuthException("请求以下资源时发生错误 " + e.Message + " " + uri, e);
            }
        }

        private static async Task<string> GetPage(string uri, CookieContainer cookies, string refer = null, Dictionary<string, string> extraHeaders = null)
        {
            try {
                var req = Forwarder.CreateRequest(uri);
                req.Method = "GET";
                req.CookieContainer = cookies;
                req.Referer = refer;

                if(extraHeaders != null) {
                    foreach(var kv in extraHeaders) {
                        req.Headers.Add(kv.Key, kv.Value);
                    }
                }

                using(var response = (HttpWebResponse)await req.GetResponseAsync())
                using(var reader = new StreamReader(response.GetResponseStream())) {
                    return await reader.ReadToEndAsync();
                }
            } catch(IOException e) {
                throw new KcAuthException("请求以下资源时发生网络错误 " + e.Message + " " + uri, e);
            } catch(WebException e) {
                throw new KcAuthException("请求以下资源时发生错误 " + e.Message + " " + uri, e);
            }
        }

        private static readonly System.Threading.ReaderWriterLockSlim infoLock = new System.Threading.ReaderWriterLockSlim();
        private static DateTimeOffset lastUpdate = DateTimeOffset.MinValue;
        private static DateTimeOffset maintenanceStart = DateTimeOffset.MaxValue;
        private static DateTimeOffset maintenanceEnd = DateTimeOffset.MinValue;
        private static bool maintenanceOngoing = true;
        private static readonly Dictionary<string, string> servers = new Dictionary<string, string>();
        private static string kcLoginUrl;
        //private static string kcTokenUrl;
        private static string kcWorldUrl;

        public static string MaintenanceMessage
        {
            get
            {
                infoLock.EnterReadLock();
                UpdateKcInfo(KcInfoCacheHours*2);
                try {
                    if(lastUpdate < maintenanceStart) {
                        if(DateTimeOffset.UtcNow > maintenanceStart) {
                            return "维护中，预计将于" + maintenanceEnd.ToString() + "结束";
                        }
                    }
                    if(maintenanceOngoing) {
                        if(DateTimeOffset.UtcNow > maintenanceEnd) {
                            return "游戏可能正在维护中，您可以尝试登陆，或者在30分钟后刷新页面";
                        }
                        return "维护中，预计将于" + maintenanceEnd.ToString() + "结束";
                    }
                    return null;
                } finally {
                    infoLock.ExitReadLock();
                }
            }
        }

        private static Task UpdateKcInfo(double cacheHour = 1)
        {
            return Task.Run(() => {
                infoLock.EnterUpgradeableReadLock();
                if((DateTimeOffset.Now - lastUpdate).TotalHours < cacheHour) {
                    infoLock.ExitUpgradeableReadLock();
                    return;
                }
                infoLock.EnterWriteLock();
                try {
                    if((DateTimeOffset.Now - lastUpdate).TotalHours < cacheHour) {
                        return;
                    }

                    Engine interpreter = new Engine(cfg => cfg
                        .LocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"))
                        .Culture(System.Globalization.CultureInfo.GetCultureInfo("ja-JP"))
                        .MaxStatements(100)
                        .LimitRecursion(2));

                    var hwr = Forwarder.CreateRequest(KcsConstantsJs);
                    hwr.Method = "GET";
                    using(var resp = hwr.GetResponse())
                    using(var jsRdr = new StreamReader(resp.GetResponseStream()))
                        interpreter.Execute(jsRdr.ReadToEnd());

                    var urlInfo = interpreter.GetValue("ConstURLInfo");
                    kcLoginUrl = interpreter.GetValue(urlInfo, "LoginURL").AsString();
                    //kcTokenUrl = interpreter.GetValue(urlInfo, "GetTokenURL").AsString();
                    kcWorldUrl = interpreter.GetValue(urlInfo, "GetUserWorldURL").AsString();

                    var maintenanceInfo = interpreter.GetValue("MaintenanceInfo");
                    maintenanceOngoing = interpreter.GetValue(maintenanceInfo, "IsDoing").AsNumber() != 0;
                    maintenanceStart = UnixTimestamp.MillisecondTimestampToDateTimeOffset(interpreter.GetValue(maintenanceInfo, "StartDateTime").AsNumber());
                    maintenanceEnd = UnixTimestamp.MillisecondTimestampToDateTimeOffset(interpreter.GetValue(maintenanceInfo, "EndDateTime").AsNumber());

                    servers.Clear();
                    var serverInfo = interpreter.GetValue("ConstServerInfo");
                    foreach(var kv in serverInfo.AsObject().GetOwnProperties()) {
                        if(kv.Value.Value?.IsString() != true) continue;
                        servers.Add(kv.Key, kv.Value.Value.Value.AsString());
                    }

                    lastUpdate = DateTimeOffset.UtcNow;
                } finally {
                    infoLock.ExitWriteLock();
                    infoLock.ExitUpgradeableReadLock();
                }
            });
        }

        [Serializable]
        public class KcAuthException : Exception
        {
            public KcAuthException() { }
            public KcAuthException(string message) : base(message) { }
            public KcAuthException(string message, Exception inner) : base(message, inner) { }
            protected KcAuthException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context)
            { }
        }
    }
}