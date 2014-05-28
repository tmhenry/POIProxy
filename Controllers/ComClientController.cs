using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using POIProxy.Models;

namespace POIProxy.Controllers
{
    public class ComClientController : Controller
    {
        private ComClientViewModel model = new ComClientViewModel
        {
            ProxyHost = POIGlobalVar.ProxyHost,
            ProxyPort = POIGlobalVar.ProxyPort
        }; 

        //
        // GET: /ComClient/

        public ActionResult Index()
        {
            return View(model);
        }

        public ActionResult Ios()
        {
            return View(model);
        }

    }
}
