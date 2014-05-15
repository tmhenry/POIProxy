using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(POIProxy.Startup))]
namespace POIProxy
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
        }
    }
}