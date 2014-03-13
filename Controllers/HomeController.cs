using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using POILibCommunication;
using POIProxy.Models;

namespace POIProxy.Controllers
{
    public class HomeController : Controller
    {
        // GET: /Home/
        public ActionResult Index()
        {
            int numWebClients = 0; // POIGlobalVar.WebUserProfiles.Count;
            int numMobileClients = 0; // POIGlobalVar.UserProfiles.Count - numWebClients;

            HomeViewModel model = new HomeViewModel
            {
                Name = "POIProxy",
                ContentServer = POIGlobalVar.ContentServerHome,
                DNSServer = POIGlobalVar.DNSServerHome,
                ProxyServerIP = POIGlobalVar.ProxyServerIP,
                ProxyServerPort = POIGlobalVar.ProxyServerPort,

                ProxyHost = POIGlobalVar.ProxyHost,
                ProxyPort = POIGlobalVar.ProxyPort,

                NumMobileClients = numMobileClients,
                NumWebClients = numWebClients,
            };

            return View(model);
        }
    }
}
