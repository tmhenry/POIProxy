using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using log4net;
using System.Reflection;

using Microsoft.AspNet.SignalR;
using System.Web.Script.Serialization;

namespace POIProxy
{
    //Define all the global variables here
    public static class POIGlobalVar
    {
        public static POIProxyKernel Kernel { get; set; }

        public static String ProxyServerIP { get; set; }
        public static int ProxyServerPort { get; set; }
        public static String ContentServerHome { get; set; }
        public static String DNSServerHome { get; set; }
        public static String Uploader { get; set; }

        public static String ProxyHost { get; set; }
        public static int ProxyPort { get; set; }
        public static String DbHost { get; set; }
        public static String DbName { get; set; }
        public static String DbUsername { get; set; }
        public static String DbPassword { get; set; }

        public static String KeywordsFileName { get { return "POI_Keywords.txt"; } }
        public static String KeywordsFileType { get { return ".txt"; } }

        public static int MaxMobileClientCount { get; set; }

        //public static POIUIScheduler Scheduler { get; set; }

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void POIDebugLog(object msg)
        {
            //Notify all the server web end about the message
            var context = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
            context.Clients.Group(@"serverLog").logMessage(msg);

            try
            {
                log.Debug(msg);
            }
            catch (Exception e)
            {
                context.Clients.Group(@"serverLog").logMessage(e);
            }
        }
    }

}