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
        POIUser myUser;

        public POIProxyPtrCtrlHandler(POIUser user)
        {
            myUser = user;
        }

        public void pointerCtrlMsgReceived(POIPointerMsg msg)
        {
            POIGlobalVar.POIDebugLog("Time is: " + msg.Timestamp);

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
            

            /*
            var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
            var session = registery.GetSessionByUser(myUser);

            session.MdArchive.LogEvent(msg);
            //Determine the color of pointer based on the user
            //To-do: !!!!!!!!!!!!!!!!!!

            //Forward the message to every other native clients
            try
            {
                foreach (POIUser user in session.Viewers)
                {
                    if (user != myUser && user.Type != UserType.WEB)
                        user.SendData(msg.getPacket(), ConType.TCP_CONTROL);
                }
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog("Error in forwarding presentation control message to native clients");
            }

            //Forward the message to web clients
            var context = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
            JavaScriptSerializer jsHandler = new JavaScriptSerializer();
            context.Clients.Group(session.Id.ToString()).scheduleMsgHandling(jsHandler.Serialize(msg));
             * */
        }
    }
}
