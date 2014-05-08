using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using POILibCommunication;
using Microsoft.AspNet.SignalR;
using System.Web.Script.Serialization;

using log4net;
using System.Reflection;

namespace POIProxy.Handlers
{
    public class POIProxyLogHandler: LogMessageDelegate
    {
        private readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public void logMessage(string msg)
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