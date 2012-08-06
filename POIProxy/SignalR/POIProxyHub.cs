using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SignalR.Hubs;
using POILibCommunication;

namespace POIProxy.SignalRFun
{
    [HubName("proxyHub")]
    public class POIProxyHub : Hub
    {
        public void EchoOnServer()
        {
            Clients.echoOnClient();
        }

        public void JoinSession(int sessionId)
        {
            Groups.Add(Context.ConnectionId, sessionId.ToString());

            //Get the initial slides
            var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
            var session = registery.GetSessionById(sessionId);

            if (session != null)
            {
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
