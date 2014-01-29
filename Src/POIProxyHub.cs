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
using System.IO;

namespace POIProxy
{
    [HubName("poiProxy")]
    public class POIProxyHub : Hub
    {
        POIProxyWBCtrlHandler webWBHandler = POIProxyGlobalVar.Kernel.myWBCtrlHandler;
        POIProxyInteractiveMsgHandler interMsgHandler = POIProxyGlobalVar.Kernel.myInterMsgHandler;
        JavaScriptSerializer jsonHandler = new JavaScriptSerializer();
        
        public void Log(string msg)
        {
            POIGlobalVar.POIDebugLog(msg);
        }

        public void updateConfigFile(string configString)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            Dictionary<string, string> configFields = js.Deserialize<Dictionary<string, string>>(configString);
            POIProxyGlobalVar.Kernel.updateConfig(configFields);

            POIGlobalVar.POIDebugLog(@"Update config file!");
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

        
        //For live session or offline session
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

            JavaScriptSerializer js = new JavaScriptSerializer();

            /*
            //Upload the json data to the content server
            try
            {
                String poiFnJson = Path.Combine(POIArchive.ArchiveHome, contentId + ".POI.json");
                String archiveFnJson = Path.Combine(POIArchive.ArchiveHome, contentId + "_" + sessionId + ".meta.json");

                using (StreamWriter writer = new StreamWriter(archiveFnJson))
                {
                    writer.Write(js.Serialize(archiveInfo));
                }

                using (StreamWriter writer = new StreamWriter(poiFnJson))
                {
                    writer.Write(js.Serialize(presInfo));
                }

                //Upload the .json to the content server
                await POIContentServerHelper.uploadContent(contentId, archiveFnJson);
                await POIContentServerHelper.uploadContent(contentId, poiFnJson);
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog("In writing json in signalr: " + e.Message);
            }
            */

            try
            {
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

        //Functions for receiving interactive messages
        public async Task textMsgReceived(string sessionId, string message)
        {
            interMsgHandler.textMsgReceived(Clients.Caller.userId, sessionId, message);

            //Notify the weixin server
            await POIProxyToWxApi.textMsgReceived(Clients.Caller.userId, sessionId, message);

            /*
            //Send push notification
            POIProxyPushNotifier.textMsgReceived(
                interMsgHandler.getUsersInSession(sessionId, Clients.Caller.userId)
            );*/

            Clients.Group("session_" + sessionId, Context.ConnectionId).
                textMsgReceived(Clients.Caller.userId, sessionId, message);
        }

        public async Task imageMsgReceived(string sessionId, string mediaId)
        {
            interMsgHandler.imageMsgReceived(Clients.Caller.userId, sessionId, mediaId);

            await POIProxyToWxApi.imageMsgReceived(Clients.Caller.userId, sessionId, mediaId);

            /*
            //Send push notification
            POIProxyPushNotifier.imageMsgReceived(
                interMsgHandler.getUsersInSession(sessionId, Clients.Caller.userId)
            );*/

            Clients.Group("session_" + sessionId, Context.ConnectionId).
                imageMsgReceived(Clients.Caller.userId, sessionId, mediaId);
        }

        public async Task voiceMsgReceived(string sessionId, string mediaId)
        {
            interMsgHandler.voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId);

            await POIProxyToWxApi.voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId);

            /*
            //Send push notification
            POIProxyPushNotifier.voiceMsgReceived(
                interMsgHandler.getUsersInSession(sessionId, Clients.Caller.userId)
            );*/

            Clients.Group("session_" + sessionId, Context.ConnectionId).
                voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId);
        }

        public async Task illustrationMsgReceived(string sessionId, string mediaId)
        {
            interMsgHandler.illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId);

            await POIProxyToWxApi.illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId);

            /*
            //Send push notification
            POIProxyPushNotifier.illustrationMsgReceived(
                interMsgHandler.getUsersInSession(sessionId, Clients.Caller.userId)
            );*/

            Clients.Group("session_" + sessionId, Context.ConnectionId).
                illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId);
        }

        public async Task createInteractiveSession(string mediaId)
        {
            //Create the session
            Tuple<string,string> result = interMsgHandler.createInteractiveSession(Clients.Caller.userId, mediaId);
            string presId = result.Item1;
            string sessionId = result.Item2;

            await Groups.Add(Context.ConnectionId, "session_" + sessionId);

            //Notify the user the connection has been created
            Clients.Caller.interactiveSessionCreated(presId, sessionId);
        }

        public async Task joinInteractiveSession(string sessionId)
        {
            if (true || interMsgHandler.checkSessionOpen(sessionId))
            {
                //Add the current connection into the session
                POIInteractiveSessionArchive archive = 
                    interMsgHandler.joinInteractiveSession(Clients.Caller.userId, sessionId);

                await Groups.Add(Context.ConnectionId, "session_" + sessionId);

                //Notify the user the join operation has been completed
                Clients.Caller.interactiveSessionJoined(sessionId, archive);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    interactiveSessionNewUserJoined(Clients.Caller.userId, sessionId);

                //Notify the wexin server about the join operation
                await POIProxyToWxApi.interactiveSessionJoined(Clients.Caller.userId, sessionId);
            }
            else
            {
                Clients.Caller.interactiveSessionJoinFailed(sessionId);
            }
        }

        public async Task endInteractiveSession(string sessionId)
        {
            //Update the database
            interMsgHandler.endInteractiveSession(sessionId);

            //Send notification to all clients in the session
            Clients.Group("session_" + sessionId, Context.ConnectionId)
                .interactiveSessionEnded(sessionId);

            //Notify the weixin server about the ending operation
            await POIProxyToWxApi.interactiveSessionEnded(Clients.Caller.userId, sessionId);
        }

        //Function called by the tutor to confirm the rating is received
        public void leaveInteractiveSession(string sessionId)
        {
            //Deregister connection id from the session group
            Groups.Remove(Context.ConnectionId, "session_" + sessionId);
        }

        public void rateAndEndInteractiveSession(string sessionId, int rating)
        {
            //Update the database
            interMsgHandler.rateInteractiveSession(sessionId, rating);

            //Send notification to all clients in the session
            Clients.Group("session_" + sessionId, Context.ConnectionId)
                .interactiveSessionRatedAndEnded(sessionId, rating);
        }

        //Event index is the last event that the client has received
        public void syncClientMessageWithSession(string sessionId, int eventIndex)
        {
            //Send the client the missed event
            Clients.Caller.interactiveSessionSynced
            (
                sessionId,
                interMsgHandler.getMissedEventsInSession(sessionId, eventIndex)
            );
        }

        #region Handle connection status change
        //When the client is joining the system for the first time
        public override System.Threading.Tasks.Task OnConnected()
        {
            //Retrieve the user information from the query string
            var info = Context.QueryString;

            String service = info["service"];
            if (service == null || service == "live")
            {
                #region handling live service
                POIGlobalVar.POIDebugLog(info["userid"]);
                String userId = info["userid"];
                POIUser user = null;

                if (userId == @"serverLog")
                {
                    Groups.Add(Context.ConnectionId, "serverLog");
                }
                else
                {
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
                }
                #endregion
            }
            else if (service == "interactive")
            {
                //For receiving server error log
                Groups.Add(Context.ConnectionId, "serverLog");
                POIGlobalVar.POIDebugLog("Interactive service");

                //Add the connection id to the user group
                Groups.Add(Context.ConnectionId, info["userId"]);

                if (info["isReconnect"] == "1")
                {
                    //Let the new connection enter the broadcast group
                    if (info["sessions"] != "")
                    {
                        List<string> sessionList = jsonHandler.Deserialize<List<string>>(info["sessions"]);
                        foreach (string sessionId in sessionList)
                        {
                            Groups.Add(Context.ConnectionId, "session_" + sessionId);
                        }
                    }

                    //Call reconnect on the client
                    Clients.Caller.clientReconnected();
                }
                
                
                //Add the connection id to the queried groups
                POIGlobalVar.POIDebugLog(info["sessions"]);
            }
            else
            {
                POIGlobalVar.POIDebugLog("Service type not recognized");
            }

            return base.OnConnected();
        }

        public override System.Threading.Tasks.Task OnDisconnected()
        {
            POIGlobalVar.POIDebugLog("Ooops, disconnected!");

            //Remove the user from the profile
            POIUser user = null;
            if (POIGlobalVar.WebConUserMap.ContainsKey(Context.ConnectionId))
            {
                user = POIGlobalVar.WebConUserMap[Context.ConnectionId];
            }
      
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
            //Handling user reconnecting
            POIGlobalVar.POIDebugLog("Client reconnected");

            //Notify the client about the reconnection, the client handles the session syncing
            Clients.Caller.clientReconnected();

            return base.OnReconnected();
        }

        #endregion
    }

   
}
