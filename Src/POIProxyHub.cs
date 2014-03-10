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
using System.Data;

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
        public async Task textMsgReceived(string sessionId, string message, double timestamp)
        {
            if (!interMsgHandler.checkSessionMsgDuplicate(sessionId, timestamp))
            {
                interMsgHandler.textMsgReceived(Clients.Caller.userId, sessionId, message, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    textMsgReceived(Clients.Caller.userId, sessionId, message, timestamp);

                //Notify the weixin server
                await POIProxyToWxApi.textMsgReceived(Clients.Caller.userId, sessionId, message);

                //Send push notification
                await POIProxyPushNotifier.textMsgReceived(
                    interMsgHandler.getUsersInSession(sessionId, Clients.Caller.userId)
                );
            }
            
        }

        public async Task imageMsgReceived(string sessionId, string mediaId, double timestamp)
        {
            if (!interMsgHandler.checkSessionMsgDuplicate(sessionId, timestamp))
            {
                interMsgHandler.imageMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    imageMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                await POIProxyToWxApi.imageMsgReceived(Clients.Caller.userId, sessionId, mediaId);

                //Send push notification
                await POIProxyPushNotifier.imageMsgReceived(
                    interMsgHandler.getUsersInSession(sessionId, Clients.Caller.userId)
                );
            }
        }

        public async Task voiceMsgReceived(string sessionId, string mediaId, double timestamp)
        {
            if (!interMsgHandler.checkSessionMsgDuplicate(sessionId, timestamp))
            {
                interMsgHandler.voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                await POIProxyToWxApi.voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId);

                //Send push notification
                await POIProxyPushNotifier.voiceMsgReceived(
                    interMsgHandler.getUsersInSession(sessionId, Clients.Caller.userId)
                );
            }
        }

        public async Task illustrationMsgReceived(string sessionId, string mediaId, double timestamp)
        {
            if (!interMsgHandler.checkSessionMsgDuplicate(sessionId, timestamp))
            {
                interMsgHandler.illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                await POIProxyToWxApi.illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId);

                //Send push notification
                await POIProxyPushNotifier.illustrationMsgReceived(
                    interMsgHandler.getUsersInSession(sessionId, Clients.Caller.userId)
                );
            }
        }

        public async Task createInteractiveSession(string mediaId, string description)
        {
            //Create the session
            Tuple<string,string> result = interMsgHandler.
                createInteractiveSession(Clients.Caller.userId, mediaId, description);
            string presId = result.Item1;
            string sessionId = result.Item2;

            POIGlobalVar.POIDebugLog("Description is " + description);

            POIGlobalVar.POIDebugLog("Session created!: " + sessionId);

            await Groups.Add(Context.ConnectionId, "session_" + sessionId);

            //Notify the user the connection has been created
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            Clients.Caller.interactiveSessionCreated(presId, sessionId, timestamp);
        }

        public async Task joinInteractiveSession(string sessionId)
        {
            if (interMsgHandler.checkSessionOpen(sessionId))
            {
                //Add the current connection into the session
                POIInteractiveSessionArchive archive = 
                    interMsgHandler.joinInteractiveSession(Clients.Caller.userId, sessionId);

                Dictionary<string, object> userInfo = interMsgHandler.getUserInfoById(Clients.Caller.userId);

                string archiveJson = jsonHandler.Serialize(archive);
                string userInfoJson = jsonHandler.Serialize(userInfo);

                await Groups.Add(Context.ConnectionId, "session_" + sessionId);

                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

                //Notify the user the join operation has been completed
                Clients.Caller.interactiveSessionJoined(sessionId, archiveJson, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    interactiveSessionNewUserJoined(Clients.Caller.userId, sessionId, userInfoJson, timestamp);

                //Notify the wexin server about the join operation
                await POIProxyToWxApi.interactiveSessionJoined(Clients.Caller.userId, sessionId, userInfoJson);
            }
            else
            {
                Clients.Caller.interactiveSessionJoinFailed(sessionId);
            }
        }

        public async Task endInteractiveSession(string sessionId)
        {
            //Update the database
            await interMsgHandler.endInteractiveSession(Clients.Caller.userId, sessionId);

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

        public async Task rateAndEndInteractiveSession(string sessionId, int rating)
        {
            //Update the database
            await interMsgHandler.rateInteractiveSession(Clients.Caller.userId, sessionId, rating);

            //Send notification to all clients in the session
            Clients.Group("session_" + sessionId, Context.ConnectionId)
                .interactiveSessionRatedAndEnded(sessionId, rating);
        }

        //Timestamp is the timestamp of the latest event received by the client
        public void syncClientMessageWithSession(string sessionId, double timestamp)
        {
            DataRow record = interMsgHandler.getSessionState(sessionId);
            string sessionState = record["status"] as string;

            POIGlobalVar.POIDebugLog("In sync timestamp is: " + timestamp);

            switch (sessionState)
            {
                case "serving":
                    //Send the client the missed event
                    Clients.Caller.interactiveSessionSynced
                    (
                        sessionId,
                        interMsgHandler.getMissedEventsInSession(sessionId, timestamp)
                    );
                    break;

                case "session_end_waiting":
                    Clients.Caller.interactiveSessionEnded(sessionId);
                    break;

                case "closed":
                    int rating = (int) record["rating"];
                    Clients.Caller.interactiveSessionRatedAndEnded(sessionId, rating);
                    break;
            }
        }

        public void rejoinSessionGroups(string sessions)
        {
            if (sessions != "")
            {
                List<string> sessionList = jsonHandler.Deserialize<List<string>>(sessions);

                POIGlobalVar.POIDebugLog("In rejoin, total sessions is :" + sessionList.Count);

                try
                {
                    foreach (string sessionId in sessionList)
                    {
                        Groups.Add(Context.ConnectionId, "session_" + sessionId);
                    }
                }
                catch (Exception e)
                {
                    POIGlobalVar.POIDebugLog(e.Message);
                }
            }
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
                POIGlobalVar.POIDebugLog("Interactive service");

                //Add the connection id to the user group
                Groups.Add(Context.ConnectionId, info["userId"]);

                if (info["isReconnect"] == "1")
                {
                    POIGlobalVar.POIDebugLog("Reconnecting from onconnected!");

                    //Let the new connection enter the broadcast group
                    if (info["sessions"] != "")
                    {
                        List<string> sessionList = jsonHandler.Deserialize<List<string>>(info["sessions"]);
                        POIGlobalVar.POIDebugLog(info["sessions"]);
                        POIGlobalVar.POIDebugLog("Total sessions is :" + sessionList.Count);

                        try
                        {
                            foreach (string sessionId in sessionList)
                            {
                                Groups.Add(Context.ConnectionId, "session_" + sessionId);
                            }
                        }
                        catch(Exception e)
                        {
                            POIGlobalVar.POIDebugLog(e.Message);
                        }
                    }

                    //Call reconnect on the client
                    Clients.Caller.clientReconnected();
                }
                
                
                //Add the connection id to the queried groups
                POIGlobalVar.POIDebugLog(info["sessions"]);
            }
            else if(service == "log")
            {
                //For receiving server error log
                Groups.Add(Context.ConnectionId, "serverLog");
                POIGlobalVar.POIDebugLog("Server log connected!");
            }
            else
            {
                POIGlobalVar.POIDebugLog("Service type not recognized");
            }

            return base.OnConnected();
        }

        public override System.Threading.Tasks.Task OnDisconnected()
        {
            var info = Context.QueryString;
            String service = info["service"];

            if (service == "interactive")
            {
                try
                {
                    //Handling user reconnecting
                    POIGlobalVar.POIDebugLog("Client " + info["userId"] + " disconnected");
                }
                catch (Exception e)
                {
                    POIGlobalVar.POIDebugLog(e.Message);
                }

            }
            
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

            var info = Context.QueryString;
            String service = info["service"];

            if (service == "interactive")
            {
                try
                {
                    //Handling user reconnecting
                    POIGlobalVar.POIDebugLog("Client " + info["userId"] + " reconnected");
                }
                catch (Exception e)
                {
                    POIGlobalVar.POIDebugLog(e.Message);
                }
                
            }

            //Notify the client about the reconnection, the client handles the session syncing
            Clients.Caller.clientReconnected();

            return base.OnReconnected();
        }

        #endregion
    }

   
}
