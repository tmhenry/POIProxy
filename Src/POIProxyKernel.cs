using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web.Script.Serialization;

using System.Configuration;
using System.Web.Configuration;

using System.Threading;
using log4net.Config;
using Qiniu.Conf;

namespace POIProxy
{
    public class POIProxyKernel
    {
        public POIProxyInteractiveMsgHandler myInterMsgHandler = new POIProxyInteractiveMsgHandler();

        public void Start()
        {
            //Load the config file into the global definition
            loadConfigFile();

            //Register a log handler to enable web logging
            XmlConfigurator.Configure();

            //Configure the qiniu storage
            Qiniu.Conf.Config.Init();            
        }

        public void loadConfigFile()
        {
            //string fn = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"poi_config");

            try
            {
                POIGlobalVar.ContentServerHome = WebConfigurationManager.AppSettings["ContentServer"];
                POIGlobalVar.DNSServerHome = WebConfigurationManager.AppSettings["DNSServer"];
                POIGlobalVar.ProxyServerIP = WebConfigurationManager.AppSettings["ProxyServerIP"];
                POIGlobalVar.ProxyServerPort = Int32.Parse(WebConfigurationManager.AppSettings["ProxyServerPort"]);

                POIGlobalVar.ProxyHost = WebConfigurationManager.AppSettings["ProxyHost"];
                POIGlobalVar.ProxyPort = Int32.Parse(WebConfigurationManager.AppSettings["ProxyPort"]);

                POIGlobalVar.DbHost = WebConfigurationManager.AppSettings["DbHost"];
                POIGlobalVar.DbName = WebConfigurationManager.AppSettings["DbName"];
                POIGlobalVar.DbUsername = WebConfigurationManager.AppSettings["DbUsername"];
                POIGlobalVar.DbPassword = WebConfigurationManager.AppSettings["DbPassword"];
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog(e);
            }
        }

    }
}
