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

        public void JoinSession(int contentId, int sessionId)
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

                try
                {
                    Clients[Context.ConnectionId].handlePresInfo(js.Serialize(curPres));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                Clients[Context.ConnectionId].startPresentation();
            }
            else
            {
                StartOfflineSession(contentId, sessionId);
            }
        }

        public void StartOfflineSession(int contentId, int sessionId)
        {
            //Read the presentation info
            POIPresentation presInfo = POIPresentation.LoadPresFromContentServer(contentId);


            //Read the metadata archive from the content server
            POIMetadataArchive archiveInfo = new POIMetadataArchive(contentId, sessionId);
            archiveInfo.ReadArchive();

            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                Clients[Context.ConnectionId].handlePresInfo(js.Serialize(presInfo));
                Clients[Context.ConnectionId].handleMetadataArchive(js.Serialize(archiveInfo.MetadataList));
                Clients[Context.ConnectionId].startPresentation();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void LeaveSession(int sessionId)
        {
            Clients[sessionId.ToString()].leave(Context.ConnectionId);
        }


    }
}
