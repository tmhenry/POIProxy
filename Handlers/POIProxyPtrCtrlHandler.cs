using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;
using System.Web.Script.Serialization;
//using SignalR;
//using SignalR.Hosting.Self;
using Microsoft.AspNet.SignalR;

namespace POIProxy.Handlers
{
    public class POIProxyPtrCtrlHandler: POIPointerCtrlMsgCB
    {

        public void pointerCtrlMsgReceived(POIPointerMsg msg, POIUser myUser)
        {
            //POIGlobalVar.POIDebugLog("Time is: " + msg.Timestamp);

            //Broadcast the event
            POISessionManager manager = POIProxyGlobalVar.Kernel.mySessionManager;
            manager.broadcastMessageToViewers(myUser, msg);

            //Log the event
            try
            {
                var session = manager.Registery.GetSessionByUser(myUser);
                session.MdArchive.LogEvent(msg);
            }
            catch (Exception e)
            {

            }
            
        }
    }
}
