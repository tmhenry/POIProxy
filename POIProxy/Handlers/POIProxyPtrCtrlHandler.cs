using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;
using System.Web.Script.Serialization;
using SignalR;
using SignalR.Hosting.Self;
using POIProxy.SignalRFun;

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
            Console.WriteLine("Time is: " + msg.Timestamp);

            var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
            var session = registery.GetSessionByUser(myUser);

            //Determine the color of pointer based on the user
            //To-do: !!!!!!!!!!!!!!!!!!

            //Forward the message to every other native clients
            try
            {
                foreach (POIUser user in session.Viewers)
                {
                    if (user != myUser)
                        user.SendData(msg.getPacket(), ConType.TCP_CONTROL);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in forwarding presentation control message to native clients");
            }

            //Forward the message to web clients
            var context = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
            JavaScriptSerializer jsHandler = new JavaScriptSerializer();
            context.Clients[session.Id.ToString()].handlePtrCtrlMsg(jsHandler.Serialize(msg));
        }
    }
}
