using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Web.Http;
using System.Web.Http.SelfHost;
using System.Net.Http;
using System.Web;
using System.Web.Routing;


using System.IO;
using RazorEngine;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using System.Reflection;

using SignalR;
using SignalR.Hosting.Self;

namespace POIProxy
{
    public class POIWebBackend
    {
        private HttpSelfHostServer mainServer;
        private Server signalRServer;

        private int mainPort = 8091;
        private int signalRPort = 8080;
        private string baseAddr = "http://192.168.1.109:";

        public bool Status { get; set; }

        public void Run()
        {
            RunSignalRServer();
            RunMainServer();
        }

        private void ShowError(string msg)
        {
            Console.WriteLine(msg);
        }

        private void RunMainServer()
        {
            string url = baseAddr + mainPort + "/";
            POIProxyGlobalVar.MainUrl = url;

            try
            {
                InitRazorEngine();

                HttpSelfHostConfiguration config = new HttpSelfHostConfiguration(url);

                //Routes for API
                config.Routes.MapHttpRoute(
                    name: "DefaultApi",
                    routeTemplate: "api/{controller}/{id}",
                    defaults: new { id = RouteParameter.Optional }
                );

                //Routes for Content
                config.Routes.MapHttpRoute(
                    name: "Content",
                    routeTemplate: "content/{controller}/{name}",
                    defaults: new { id = RouteParameter.Optional }
                );

                mainServer = new HttpSelfHostServer(config);
                mainServer.OpenAsync().Wait();
                Status = true;
            }
            catch (Exception e)
            {
                Status = false;
                ShowError("Something happened!");
            }

            
            //var client = new HttpClient() { BaseAddress = new Uri(url) };
            //Console.WriteLine("Client received: {0}", client.GetStringAsync("api/Home").Result);
        }

        private void RunSignalRServer()
        {
            string url = baseAddr + signalRPort + "/";
            POIProxyGlobalVar.SignalRUrl = url;

            try
            {
                var signalRServer = new Server(url);
                signalRServer.MapHubs();

                signalRServer.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Something happened!");
            }

        }

        private void InitRazorEngine()
        {
            //The resolver configuration
            //To do: get the project name dynamically
            string viewPathTemplate = "POIProxy.Views.{0}";
            TemplateServiceConfiguration templateConfig = new TemplateServiceConfiguration();
            templateConfig.Resolver = new DelegateTemplateResolver(name =>
            {
                string resourcePath = string.Format(viewPathTemplate, name);
                var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            });

            Razor.SetTemplateService(new TemplateService(templateConfig));
        }
    }
}
