﻿using System;
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
    public class POIProxyPresCtrlHandler : POIPresentationControlMsgCB
    {
        POIUser myUser;

        public POIProxyPresCtrlHandler(POIUser user)
        {
            myUser = user;
        }

        public void presCtrlMsgReceived(POIPresCtrlMsg msg)
        {
            var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
            var session = registery.GetSessionByUser(myUser);

            //Handle the msg locally
            HandlePresCtrlMsgByProxy(session, msg);

            //Forward the message to every other native clients
            try
            {
                foreach (POIUser user in session.Viewers)
                {
                    if(user != myUser)
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
            context.Clients[session.Id.ToString()].handlePresCtrlMsg(jsHandler.Serialize(msg));         
        }

        private void HandlePresCtrlMsgByProxy(POISession session, POIPresCtrlMsg msg)
        {
            var presController = session.PresController;

            switch((PresCtrlType)msg.CtrlType)
            {
                case PresCtrlType.Next:
                    presController.playNext();
                    break;

                case PresCtrlType.Prev:
                    presController.playPrev();
                    break;

                default:
                    Console.WriteLine("Presentation ctrl type not recognized");
                    break;
            }
        }
    }
}
