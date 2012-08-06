using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;
using SignalR;
using SignalR.Hosting.Self;

using POIProxy.SignalRFun;
using System.Web.Script.Serialization;

namespace POIProxy.Handlers
{
    public class POIProxyWBCtrlHandler : POIWhiteBoardMsgCB, POICommentCB
    {
        POIUser myUser;

        public void showWhiteBoard() { }
        public void hideWhiteBoard() { }

        public POIProxyWBCtrlHandler(POIUser user)
        {
            myUser = user;
        }

        public void whiteboardCtrlMsgReceived(POIWhiteboardMsg msg)
        {
            var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
            var session = registery.GetSessionByUser(myUser);

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
                Console.WriteLine("Error in forwarding whiteboard control message to native clients");
            }

            //Forward the message to web clients
            var context = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
            JavaScriptSerializer jsHandler = new JavaScriptSerializer();
            context.Clients[session.Id.ToString()].handleWhiteboardMsg(jsHandler.Serialize(msg));
        }

        public void handleComment(POIComment comment)
        {
            //Forward the message to every other native clients
            try
            {
                foreach (POIUser user in POIGlobalVar.UserProfiles.Values)
                {
                    if (user != myUser)
                        user.SendData(comment.getPacket(), ConType.TCP_CONTROL);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in forwarding comment message to native clients");
            }

            //Forward the message to web clients
            var context = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
            JavaScriptSerializer jsHandler = new JavaScriptSerializer();
            context.Clients.handleCommentMsg(jsHandler.Serialize(comment));
        }
    }
}
