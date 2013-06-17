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

using System.Threading.Tasks;

namespace POIProxy
{
    [HubName("poiProxy")]
    public class POIProxyHub : Hub
    {
        POIProxyWBCtrlHandler webWBHandler = POIProxyGlobalVar.Kernel.myWBCtrlHandler;
        
        public void Log(string msg)
        {
            POIGlobalVar.POIDebugLog(msg);
        }

        public void HandleCommentMsgOnServer(string msg)
        {
            //Get the current user
            POIUser curUser = POIGlobalVar.WebConUserMap[Context.ConnectionId];
            if (curUser != null)
            {
                webWBHandler.handleStringComment(msg, curUser);
            }
            else
            {
                POIGlobalVar.POIDebugLog("Cannot find the user associated with the connection");
            }
        }

        public async Task JoinSession(int contentId, int sessionId)
        {
            //Get the session
            var manager = POIProxyGlobalVar.Kernel.mySessionManager;
            var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
            var session = registery.GetSessionById(sessionId);

            if (session != null)
            {
                //Use the current user to enter the session registery
                POIUser curUser = POIGlobalVar.WebConUserMap[Context.ConnectionId];
                if (curUser == null)
                {
                    POIGlobalVar.POIDebugLog("Cannot find the web user associated with connection");
                    return;
                }

                //Join the session
                manager.JoinSession(curUser, contentId, sessionId);
                await Groups.Add(Context.ConnectionId, sessionId.ToString());

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
                await StartOfflineSession(contentId, sessionId);
            }
        }

        public async Task StartOfflineSession(int contentId, int sessionId)
        {
            POIPresentation presInfo;
            POIMetadataArchive archiveInfo;


            POIOfflineSessionCache cache = POIProxyGlobalVar.Kernel.mySessionManager.Cache;
            Tuple<POIPresentation, POIMetadataArchive> cacheResult = cache.SearchSessionInfoInCache(contentId, sessionId);

            cacheResult = null;

            if (cacheResult == null)
            {
                //Read the presentation info
                presInfo = await POIPresentation.LoadPresFromContentServer(contentId);

                //Read the metadata archive from the content server
                archiveInfo = new POIMetadataArchive(contentId, sessionId);
                await archiveInfo.ReadArchive();

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

                //Disconnect the client for better performance
                Clients.Caller.disconnectFromProxy();
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog(e);
            }
        }

        public void LeaveSession(int sessionId)
        {
            Clients.Group(sessionId.ToString()).leave(Context.ConnectionId);
            
            //Get the session
            var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
            var session = registery.GetSessionById(sessionId);
            POIUser curUser = POIGlobalVar.WebConUserMap[Context.ConnectionId];

            if (session != null && curUser != null)
            {
                session.LeaveAsViewer(curUser);
            }
        }



        #region Handle connection status change
        //When the client is joining the system for the first time
        public override System.Threading.Tasks.Task OnConnected()
        {
            //Retrieve the user information from the query string
            var info = Context.QueryString;
            POIGlobalVar.POIDebugLog(info["userid"]);
            POIGlobalVar.POIDebugLog(info["email"]);

            String userId = info["userid"];
            POIUser user = null;

            //Check if the user exists
            if (POIGlobalVar.WebUserProfiles.ContainsKey(userId))
            {
                //Set the connectionId to user mapping
                user = POIGlobalVar.WebUserProfiles[userId];
            }
            else
            {
                //Create the user and set the mapping
                user = new POIUser(UserType.WEB);
                user.UserID = userId;

                POIGlobalVar.WebUserProfiles[userId] = user;
            }

            //Set the connection to user mapping
            POIGlobalVar.WebConUserMap[Context.ConnectionId] = user;
            
            //Let the user know the authentication is done
            Clients.Caller.handleUserAuthenticated();

            return base.OnConnected();
        }

        public override System.Threading.Tasks.Task OnDisconnected()
        {
            POIGlobalVar.POIDebugLog("Ooops, disconnected!");

            //Remove the user from the profile
            POIUser user = POIGlobalVar.WebConUserMap[Context.ConnectionId];
            if (user != null)
            {
                try
                {
                    //Remove the user from the profiles
                    POIGlobalVar.WebUserProfiles.Remove(user.UserID);
                    POIGlobalVar.WebConUserMap.Remove(Context.ConnectionId);
                }
                catch (Exception e)
                {
                    POIGlobalVar.POIDebugLog(e);
                }
            }

            return base.OnDisconnected();
        }

        public override System.Threading.Tasks.Task OnReconnected()
        {
            return base.OnReconnected();
        }

        #endregion
    }
}
