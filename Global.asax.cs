using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;

using System.Threading;
using Parse;

using System.Web.Configuration;
using Microsoft.AspNet.SignalR;

namespace POIProxy
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            Thread kernelThread = new Thread(StartKernelThread);
            kernelThread.Start();

            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            ParseClient.Initialize(WebConfigurationManager.AppSettings["ParseAppId"], 
                WebConfigurationManager.AppSettings["ParseAppKey"]);

            GlobalHost.Configuration.ConnectionTimeout = TimeSpan.FromMinutes(20);
            GlobalHost.Configuration.DisconnectTimeout = TimeSpan.FromMinutes(20);
        }

        private void StartKernelThread()
        {
            POIGlobalVar.Kernel = new POIProxyKernel();
            POIGlobalVar.Kernel.Start();
        }
    }
}