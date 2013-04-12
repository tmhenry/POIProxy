using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Web.Http;
using System.Net.Http;
using RazorEngine;
using RazorEngine.Templating;

using POILibCommunication;

namespace POIProxy.Controllers
{
    public class HomeController : ApiController
    {
        public HttpResponseMessage Get()
        {
            bool userLogin = true;

            var model = new 
            { 
                Name = "POI Web End", 
                Email = "someone@somewhere.com", 
                SignalrUrl = POIProxyGlobalVar.SignalRUrl + "signalr/",
                ScriptUrl = POIProxyGlobalVar.MainUrl + "content/Scripts/",
                ImageUrl = POIProxyGlobalVar.MainUrl + "content/Images/",
                VideoUrl = POIProxyGlobalVar.MainUrl + "content/Videos/",
                AudioUrl = POIProxyGlobalVar.MainUrl + "content/Audios/",
                AudioStreamingUrl = POIProxyGlobalVar.AudioStreamingUrl,
                StreamingUrl = POIProxyGlobalVar.MainUrl + "content/Streaming/",
                CSSUrl = POIProxyGlobalVar.MainUrl + "content/CSS/",
                SessionUrl = POIProxyGlobalVar.MainUrl + "api/Sessions",
                ContentServerUrl = POIGlobalVar.ContentServerHome + "content.php",
            };

            string result;

            if (userLogin)
            {
                result = Razor.Resolve("Index.cshtml", model).Run(new ExecuteContext());
            }
            else
            {
                result = Razor.Resolve("Login.cshtml", model).Run(new ExecuteContext());
            }  

            var response = new HttpResponseMessage();
            response.Content = new StringContent(result, System.Text.Encoding.UTF8, "text/html");

            return response;
        }
    }
}
