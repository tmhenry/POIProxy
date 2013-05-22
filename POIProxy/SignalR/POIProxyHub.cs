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

                Clients[Context.ConnectionId].setAudioSyncReference(POIContentServerHelper.getAudioSyncReference(0, 0));

                Clients[Context.ConnectionId].startPresentation();
            }
            else
            {
                StartOfflineSession(contentId, sessionId);
            }
        }

        public void StartOfflineSession(int contentId, int sessionId)
        {
            POIPresentation presInfo;
            POIMetadataArchive archiveInfo;


            POIOfflineSessionCache cache = POIProxyGlobalVar.Kernel.mySessionManager.Cache;
            Tuple<POIPresentation, POIMetadataArchive> cacheResult = cache.SearchSessionInfoInCache(contentId, sessionId);

            if (cacheResult == null)
            {
                //Read the presentation info
                presInfo = POIPresentation.LoadPresFromContentServer(contentId);

                //Read the metadata archive from the content server
                archiveInfo = new POIMetadataArchive(contentId, sessionId);
                archiveInfo.ReadArchive();

                cache.AddRecordToSessionCache(contentId, sessionId, presInfo, archiveInfo);
            }
            else
            {
                presInfo = cacheResult.Item1;
                archiveInfo = cacheResult.Item2;
            }
            
            

            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer();

                //Get the audio reference for the archive
                Dictionary<string, string> jsonResponse = js.Deserialize(
                    POIContentServerHelper.getAudioSyncReference(0, 0), 
                    typeof(Dictionary<string, string>)
                ) as Dictionary<string, string>;

                if (jsonResponse["starttime"] == "")
                {
                    archiveInfo.AudioTimeReference = archiveInfo.SessionTimeReference;
                }
                else
                {
                    archiveInfo.AudioTimeReference = Double.Parse(jsonResponse["starttime"]);
                }

                

                Clients[Context.ConnectionId].handlePresInfo(js.Serialize(presInfo));
                Clients[Context.ConnectionId].handleMetadataArchive(js.Serialize(archiveInfo));
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
