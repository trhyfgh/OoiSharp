using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(OoiSharp.Startup))]
namespace OoiSharp
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
