using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

using Microsoft.AspNet.SignalR;

namespace POIProxy
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            ////Register signalr hubs
            //RouteTable.Routes.MapHubs(new HubConfiguration { 
            //    EnableCrossDomain = true,
            //    EnableDetailedErrors = true
            //});
            //RouteTable.Routes.MapHubs(new HubConfiguration());

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}