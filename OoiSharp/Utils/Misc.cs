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
    internal static class Misc
    {
        public static void CheckConfig(ViewModelBase mdl)
        {
            if(Cookie.IsHmacKeyTemporary) {
                mdl.Warn.Add("验证密钥未正确配置，已启用临时密钥。可能无法保持登陆状态。");
            }
            if(!Forwarder.IsConfigurationValid) {
                mdl.Error.Add("转发器配置无效，无法处理任何请求。");
            }
            var maintenance = KcAuth.MaintenanceMessage;
            if(maintenance != null) {
                mdl.Info.Add(maintenance);
            }
            if(!Handlers.KcsApiStart2Handler.IsCachedStart2Avaliable) {
                mdl.Info.Add("当前api_start2的缓存副本不可用。于10分钟以前登陆的用户可能无法进入游戏。");
            }
        }
    }
}