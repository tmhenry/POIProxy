using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SignalR.Hubs;
using POILibCommunication;
using POIProxy.Handlers;
using System.Web.Script.Serialization;

namespace POIProxy.SignalRFun
{
    [HubName("proxyHub")]
    public class POIProxyHub : Hub
    {
        POIProxyWBCtrlHandler webWBHandler = new POIProxyWBCtrlHandler(new POIUser());

        public void EchoOnServer()
        {
            Clients.echoOnClient();
        }

        public void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        public void HandleCommentMsgOnServer(string msg)
        {
            webWBHandler.handleStringComment(msg);
        }

        public void JoinSession(int sessionId)
        {
            Groups.Add(Context.ConnectionId, sessionId.ToString());

            //Get the initial slides
            var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
            var session = registery.GetSessionById(sessionId);

            if (session != null)
            {
                //Get the presentation file and send to the user
                POIPresentation curPres = session.PresController.CurPres;
                JavaScriptSerializer js = new JavaScriptSerializer();
                Clients[Context.ConnectionId].handlePresInfo(js.Serialize(curPres));

                List<POISlide> initialSlides = session.PresController.GetInitialSlides();
                for (int i = 0; i < initialSlides.Count; i++)
                {
                    POISlide slide = initialSlides[i];

                    Clients[Context.ConnectionId].getSlide(slide.Index);
                }

                Clients[Context.ConnectionId].startPresentation();
            }
        }

        public void LeaveSession(int sessionId)
        {
            Clients[sessionId.ToString()].leave(Context.ConnectionId);
        }


    }
}
