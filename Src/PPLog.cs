using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using log4net;
using System.Reflection;

using Microsoft.AspNet.SignalR;

namespace POIProxy
{
    public static class PPLog
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void debugLog(object msg)
        {
            handleLog(msg, "debug");
        }

        public static void infoLog(object msg)
        {
            handleLog(msg, "info");
        }

        public static void warnLog(object msg)
        {
            handleLog(msg, "warn");
        }

        public static void errorLog(object msg)
        {
            handleLog(msg, "error");
        }

        public static void fatalLog(object msg)
        {
            handleLog(msg, "fatal");
        }

        private static void handleLog(object msg, string type)
        {
            string dateTime = DateTime.Now.ToString("HH:mm:ss");
            msg = "[" + dateTime + "]" + " " + type + ":  " + msg;

            var context = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
            context.Clients.Group(@"serverLog").logMessage(msg);
            try
            {
                switch (type)
                { 
                    case "debug":
                        log.Debug(msg);
                        break;

                    case "info":
                        log.Info(msg);
                        break;
                    
                    case "warn":
                        log.Warn(msg);
                        break;

                    case "error":
                        log.Error(msg);
                        break;

                    case "fatal":
                        log.Fatal(msg);
                        break;
                }
                
            }
            catch (Exception e)
            {
                context.Clients.Group(@"serverLog").logMessage(e);
            } 
        }
    }
}