using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using POILibCommunication;
using Microsoft.AspNet.SignalR;
using System.Web.Script.Serialization;

namespace POIProxy.Handlers
{
    public class POIProxyLogHandler: LogMessageDelegate
    {
        public void logMessage(string msg)
        {
            //Notify all the server web end about the message
            var context = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
            context.Clients.Group(@"serverLog").logMessage(msg);
        }
    }
}