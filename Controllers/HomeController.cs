﻿using System;
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
            int numWebClients = POIGlobalVar.WebUserProfiles.Count;
            int numMobileClients = POIGlobalVar.UserProfiles.Count - numWebClients;

            HomeViewModel model = new HomeViewModel
            {
                Name = "POIProxy",
                ContentServer = POIGlobalVar.ContentServerHome,
                DNSServer = POIGlobalVar.DNSServerHome,
                ProxyServerIP = POIGlobalVar.ProxyServerIP,
                ProxyServerPort = POIGlobalVar.ProxyServerPort,

                NumMobileClients = numMobileClients,
                NumWebClients = numWebClients,
            };

            return View(model);
        }
    }
}