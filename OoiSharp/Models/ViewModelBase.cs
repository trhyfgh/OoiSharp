using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OoiSharp.Models
{
    public class ViewModelBase
    {
        public List<string> Info { get; } = new List<string>();
        public List<string> Error { get; } = new List<string>();
        public List<string> Warn { get; } = new List<string>();

        public ViewModelBase()
        {
            Utils.Misc.CheckConfig(this);
        }
    }
}