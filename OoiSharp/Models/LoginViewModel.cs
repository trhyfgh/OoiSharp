using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OoiSharp.Models
{
    public class LoginViewModel : ViewModelBase
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public uint SessionTime { get; set; } = 43200000;
        public bool IpBound { get; set; } = true;

        public static readonly SelectListItem[] SessionTimeSelections = new SelectListItem[] {
            new SelectListItem() { Value = "43200000", Selected = true, Text = "12小时" },
            new SelectListItem() { Value = "86400000", Selected = false, Text = "24小时" },
            new SelectListItem() { Value = "259200000", Selected = false, Text = "3天" },
            new SelectListItem() { Value = "604800000", Selected = false, Text = "7天" },
            new SelectListItem() { Value = "1296000000", Selected = false, Text = "15天" },
            new SelectListItem() { Value = "2592000000", Selected = false, Text = "30天" },
            new SelectListItem() { Value = "4294967295", Selected = false, Text = "50天" },
        };
    }
}