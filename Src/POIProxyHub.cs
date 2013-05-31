using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//using SignalR.Hubs;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Owin;

using POILibCommunication;
using POIProxy.Handlers;
using System.Web.Script.Serialization;

namespace POIProxy
{
    [HubName("poiProxy")]
    public class POIProxyHub : Hub
    {
        POIProxyWBCtrlHandler webWBHandler = new POIProxyWBCtrlHandler(new POIUser());

        public void EchoOnServer()
        {
            Clients.All.echoOnClient();
        }

        
        public void Log(string msg)
        {
            POIGlobalVar.POIDebugLog(msg);
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
                    Clients.Caller.handlePresInfo(js.Serialize(curPres));
                    //Clients[Context.ConnectionId].handlePresInfo(js.Serialize(curPres));
                }
                catch (Exception e)
                {
                    POIGlobalVar.POIDebugLog(e);
                }

                Clients.Caller.setAudioSyncReference(session.MdArchive.AudioTimeReference);
                Clients.Caller.startPresentation();
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

            cacheResult = null;

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
                

                Clients.Caller.handlePresInfo(js.Serialize(presInfo));
                Clients.Caller.handleMetadataArchive(js.Serialize(archiveInfo));
                Clients.Caller.startPresentation();
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog(e);
            }
        }

        public void LeaveSession(int sessionId)
        {
            Clients.Group(sessionId.ToString()).leave(Context.ConnectionId);
        }
        

    }
}
