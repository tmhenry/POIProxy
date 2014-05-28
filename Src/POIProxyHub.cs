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
        static POIProxyInteractiveMsgHandler interMsgHandler = POIProxyGlobalVar.Kernel.myInterMsgHandler;
        JavaScriptSerializer jsonHandler = new JavaScriptSerializer();
        
        public void Log(string msg)
        {
            POIGlobalVar.POIDebugLog(msg);

            try
            {
                Clients.Caller.logMsg("hi hi");
                POIGlobalVar.POIDebugLog("Call completed");
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog(e.Message);
            }
            
        }

        //public void updateConfigFile(string configString)
        //{
        //    JavaScriptSerializer js = new JavaScriptSerializer();
        //    Dictionary<string, string> configFields = js.Deserialize<Dictionary<string, string>>(configString);
        //    POIProxyGlobalVar.Kernel.updateConfig(configFields);

        //    POIGlobalVar.POIDebugLog(@"Update config file!");
        //}

        //public void HandleCommentMsgOnServer(string msg)
        //{
        //    //Get the current user
        //    POIUser curUser = POIGlobalVar.WebConUserMap[Context.ConnectionId];
        //    if (curUser != null)
        //    {
        //        webWBHandler.handleStringComment(msg, curUser);
        //    }
        //    else
        //    {
        //        POIGlobalVar.POIDebugLog("Cannot find the user associated with the connection");
        //    }
        //}

        
        ////For live session or offline session
        //public async Task JoinSession(int contentId, int sessionId)
        //{
        //    //Get the session
        //    var manager = POIProxyGlobalVar.Kernel.mySessionManager;
        //    var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
        //    var session = registery.GetSessionById(sessionId);

        //    if (session != null)
        //    {
        //        //Use the current user to enter the session registery
        //        POIUser curUser = POIGlobalVar.WebConUserMap[Context.ConnectionId];
        //        if (curUser == null)
        //        {
        //            POIGlobalVar.POIDebugLog("wrong connection!");
        //            POIGlobalVar.POIDebugLog("Cannot find the web user associated with connection");
        //            return;
        //        }

        //        //Join the session
        //        manager.JoinSession(curUser, contentId, sessionId);
        //        await Groups.Add(Context.ConnectionId, sessionId.ToString());

        //        //Get the presentation file and send to the user
        //        POIPresentation curPres = session.PresController.CurPres;
        //        JavaScriptSerializer js = new JavaScriptSerializer();

        //        try
        //        {
        //            Clients.Caller.handlePresInfo(js.Serialize(curPres));
        //            //Clients[Context.ConnectionId].handlePresInfo(js.Serialize(curPres));
        //        }
        //        catch (Exception e)
        //        {
        //            POIGlobalVar.POIDebugLog(e);
        //        }

        //        Clients.Caller.setAudioSyncReference(session.MdArchive.AudioTimeReference);
        //        Clients.Caller.startPresentation();
        //    }
        //    else
        //    {
        //        await StartOfflineSession(contentId, sessionId);
        //    }
        //}

        //public async Task StartOfflineSession(int contentId, int sessionId)
        //{
        //    POIPresentation presInfo;
        //    POIMetadataArchive archiveInfo;


        //    POIOfflineSessionCache cache = POIProxyGlobalVar.Kernel.mySessionManager.Cache;
        //    Tuple<POIPresentation, POIMetadataArchive> cacheResult = cache.SearchSessionInfoInCache(contentId, sessionId);

        //    cacheResult = null;

        //    if (cacheResult == null)
        //    {
        //        //Read the presentation info
        //        presInfo = await POIPresentation.LoadPresFromContentServer(contentId);

        //        //Read the metadata archive from the content server
        //        archiveInfo = new POIMetadataArchive(contentId, sessionId);
        //        await archiveInfo.ReadArchive();

        //        cache.AddRecordToSessionCache(contentId, sessionId, presInfo, archiveInfo);
        //    }
        //    else
        //    {
        //        presInfo = cacheResult.Item1;
        //        archiveInfo = cacheResult.Item2;
        //    }

        //    JavaScriptSerializer js = new JavaScriptSerializer();

        //    /*
        //    //Upload the json data to the content server
        //    try
        //    {
        //        String poiFnJson = Path.Combine(POIArchive.ArchiveHome, contentId + ".POI.json");
        //        String archiveFnJson = Path.Combine(POIArchive.ArchiveHome, contentId + "_" + sessionId + ".meta.json");

        //        using (StreamWriter writer = new StreamWriter(archiveFnJson))
        //        {
        //            writer.Write(js.Serialize(archiveInfo));
        //        }

        //        using (StreamWriter writer = new StreamWriter(poiFnJson))
        //        {
        //            writer.Write(js.Serialize(presInfo));
        //        }

        //        //Upload the .json to the content server
        //        await POIContentServerHelper.uploadContent(contentId, archiveFnJson);
        //        await POIContentServerHelper.uploadContent(contentId, poiFnJson);
        //    }
        //    catch (Exception e)
        //    {
        //        POIGlobalVar.POIDebugLog("In writing json in signalr: " + e.Message);
        //    }
        //    */

        //    try
        //    {
        //        Clients.Caller.handlePresInfo(js.Serialize(presInfo));
        //        Clients.Caller.handleMetadataArchive(js.Serialize(archiveInfo));
        //        Clients.Caller.startPresentation();

        //        //Disconnect the client for better performance
        //        Clients.Caller.disconnectFromProxy();
        //    }
        //    catch (Exception e)
        //    {
        //        POIGlobalVar.POIDebugLog(e);
        //    }
        //}

        //public void LeaveSession(int sessionId)
        //{
        //    Clients.Group(sessionId.ToString()).leave(Context.ConnectionId);
            
        //    //Get the session
        //    var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
        //    var session = registery.GetSessionById(sessionId);
        //    POIUser curUser = POIGlobalVar.WebConUserMap[Context.ConnectionId];

        //    if (session != null && curUser != null)
        //    {
        //        session.LeaveAsViewer(curUser);
        //    }
        //}

        //Functions for receiving interactive messages

        public async Task textMsgReceived(string sessionId, string message, double timestamp)
        {
            POIGlobalVar.POIDebugLog("Text received: " + message + " , session is :" + sessionId + " ," + timestamp);

            if (!interMsgHandler.checkSessionMsgDuplicate(sessionId, timestamp))
            {
                interMsgHandler.textMsgReceived(Clients.Caller.userId, sessionId, message, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    textMsgReceived(Clients.Caller.userId, sessionId, message, timestamp);

                Clients.Caller.msgAckReceived(sessionId, timestamp);

                

                //Notify the weixin server
                await POIProxyToWxApi.textMsgReceived(Clients.Caller.userId, sessionId, message);

                //Send push notification
                await POIProxyPushNotifier.textMsgReceived(sessionId);
            }
            else
            {
                POIGlobalVar.POIDebugLog("Message duplicate: timestamp is " + timestamp);
                //Message duplicate: send back the ack
                Clients.Caller.msgAckReceived(sessionId, timestamp);
            }
            
        }

        public async Task imageMsgReceived(string sessionId, string mediaId, double timestamp)
        {
            if (!interMsgHandler.checkSessionMsgDuplicate(sessionId, timestamp))
            {
                interMsgHandler.imageMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    imageMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Caller.msgAckReceived(sessionId, timestamp);

                await POIProxyToWxApi.imageMsgReceived(Clients.Caller.userId, sessionId, mediaId);

                //Send push notification
                await POIProxyPushNotifier.imageMsgReceived(sessionId);
            }
            else
            {
                POIGlobalVar.POIDebugLog("Message duplicate: timestamp is " + timestamp);
                //Message duplicate: send back the ack
                Clients.Caller.msgAckReceived(sessionId, timestamp);
            }
        }

        public async Task voiceMsgReceived(string sessionId, string mediaId, double timestamp)
        {
            if (!interMsgHandler.checkSessionMsgDuplicate(sessionId, timestamp))
            {
                interMsgHandler.voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Caller.msgAckReceived(sessionId, timestamp);

                await POIProxyToWxApi.voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId);

                //Send push notification
                await POIProxyPushNotifier.voiceMsgReceived(sessionId);
            }
            else
            {
                POIGlobalVar.POIDebugLog("Message duplicate: timestamp is " + timestamp);
                //Message duplicate: send back the ack
                Clients.Caller.msgAckReceived(sessionId, timestamp);
            }
        }

        public async Task illustrationMsgReceived(string sessionId, string mediaId, double timestamp)
        {
            if (!interMsgHandler.checkSessionMsgDuplicate(sessionId, timestamp))
            {
                interMsgHandler.illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Caller.msgAckReceived(sessionId, timestamp);

                await POIProxyToWxApi.illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId);

                //Send push notification
                await POIProxyPushNotifier.illustrationMsgReceived(sessionId);
            }
            else
            {
                POIGlobalVar.POIDebugLog("Message duplicate: timestamp is " + timestamp);
                //Message duplicate: send back the ack
                Clients.Caller.msgAckReceived(sessionId, timestamp);
            }
        }

        public async Task createInteractiveSession(string mediaId, string description)
        {
            POIGlobalVar.POIDebugLog("Creator id is : " + Clients.Caller.userId);

            //Create the session
            Tuple<string,string> result = interMsgHandler.
                createInteractiveSession(Clients.Caller.userId, mediaId, description);
            string presId = result.Item1;
            string sessionId = result.Item2;

            POIGlobalVar.POIDebugLog("Description is " + description);

            POIGlobalVar.POIDebugLog("Session created!: " + sessionId);

            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            POIProxySessionManager.subscribeSession(sessionId, Clients.Caller.userId);
            await Groups.Add(Context.ConnectionId, "session_" + sessionId);

            //Notify the user the connection has been created
            Clients.Caller.interactiveSessionCreated(presId, sessionId, timestamp);

            //Make the session open after everything is ready
            interMsgHandler.updateSessionStatus(sessionId, "open");

            await POIProxyPushNotifier.sessionCreated(sessionId);
        }

        public async Task joinInteractiveSession(string sessionId)
        {
            POIInteractiveSessionArchive archive = interMsgHandler.getArchiveBySessionId(sessionId);

            if (double.Parse(archive.Info["create_at"])
                >= POITimestamp.ConvertToUnixTimestamp(DateTime.Now.AddSeconds(-60)))
            {
                POIGlobalVar.POIDebugLog("Cannot join the session, not passing time limit");
                Clients.Caller.interactiveSessionJoinBeforeStarted(sessionId);
            }
            else if (POIProxySessionManager.checkUserInSession(sessionId, Clients.Caller.userId))
            {
                //User already in the session
                POIGlobalVar.POIDebugLog("Session already joined");

                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                string archiveJson = jsonHandler.Serialize(archive);

                await Groups.Add(Context.ConnectionId, "session_" + sessionId);

                POIGlobalVar.POIDebugLog(archiveJson);

                //Send the archive to the user
                Clients.Caller.interactiveSessionJoined(sessionId, archiveJson, timestamp);
            }
            else if (POIProxySessionManager.acquireSessionToken(sessionId, 10))
            {
                POIGlobalVar.POIDebugLog("Session is open, joined!");
                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

                interMsgHandler.joinInteractiveSession(Clients.Caller.userId, sessionId, timestamp);
                //var userInfo = interMsgHandler.getUserInfoById(Clients.Caller.userId);
                var userInfo = POIProxySessionManager.getUserInfo(Clients.Caller.userId);

                string archiveJson = jsonHandler.Serialize(archive);
                string userInfoJson = jsonHandler.Serialize(userInfo);

                POIProxySessionManager.subscribeSession(sessionId, Clients.Caller.userId);
                await Groups.Add(Context.ConnectionId, "session_" + sessionId);

                POIGlobalVar.POIDebugLog(archiveJson);

                //Notify the user the join operation has been completed
                Clients.Caller.interactiveSessionJoined(sessionId, archiveJson, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    interactiveSessionNewUserJoined(Clients.Caller.userId, sessionId, userInfoJson, timestamp);

                //Notify the wexin server about the join operation
                await POIProxyToWxApi.interactiveSessionNewUserJoined(Clients.Caller.userId, sessionId, userInfoJson);

                //Send push notification
                await POIProxyPushNotifier.sessionJoined(sessionId);
            }
            else
            {
                POIGlobalVar.POIDebugLog("Cannot join the session, taken by others");
                Clients.Caller.interactiveSessionJoinFailed(sessionId);
            }
        }

        public async Task endInteractiveSession(string sessionId)
        {
            if (interMsgHandler.checkSessionServing(sessionId))
            {
                //Update the database
                interMsgHandler.endInteractiveSession(Clients.Caller.userId, sessionId);

                //Send notification to all clients in the session
                Clients.Group("session_" + sessionId, Context.ConnectionId)
                    .interactiveSessionEnded(Clients.Caller.userId, sessionId);

                //Notify the weixin server about the ending operation
                await POIProxyToWxApi.interactiveSessionEnded(Clients.Caller.userId, sessionId);

                //Send push notification
                await POIProxyPushNotifier.sessionEnded(sessionId);
            }
            else
            {
                POIGlobalVar.POIDebugLog("End a session that is not serving");
            }
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
            interMsgHandler.rateInteractiveSession(Clients.Caller.userId, sessionId, rating);

            //Send notification to all clients in the session
            Clients.Group("session_" + sessionId, Context.ConnectionId)
                .interactiveSessionRatedAndEnded(Clients.Caller.userId, sessionId, rating);

            //Send push notification
            await POIProxyPushNotifier.sessionRated(sessionId, rating);
        }

        public async Task reraiseInteractiveSession(string sessionId)
        {
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            string newSessionId = interMsgHandler.duplicateInteractiveSession(sessionId, timestamp);
            await interMsgHandler.reraiseInteractiveSession(Clients.Caller.userId, sessionId, newSessionId, timestamp);

            //Add the student to the session group
            await Groups.Add(Context.ConnectionId, "session_" + newSessionId);

            //Notify the student about interactive session reraised
            Clients.Caller.interactiveSessionReraised(sessionId, newSessionId);

            //Notify the signalr users about the cancel operation
            Clients.Group("session_" + sessionId, Context.ConnectionId)
                .interactiveSessionCancelled(sessionId);

            //Notify the weixin users about the cancel operation
            await POIProxyToWxApi.interactiveSessionCancelled(Clients.Caller.userId, sessionId);

            //Make the session open after everything is ready
            interMsgHandler.updateSessionStatus(newSessionId, "open");

            await POIProxyPushNotifier.sessionCreated(newSessionId);
        }

        public async Task syncClient(string sessionListJson)
        {
            string userId = Clients.Caller.userId;
            POIGlobalVar.POIDebugLog("sync client " + userId);

            POIGlobalVar.POIDebugLog("session list from client is :" + sessionListJson);

            Dictionary<string, double> sessionList = jsonHandler.
                Deserialize<Dictionary<string, double>>(sessionListJson);

            try
            {
                foreach (string sid in sessionList.Keys)
                {
                    //Update sync time reference
                    POIProxySessionManager.updateSyncReference(sid, userId, sessionList[sid]);
                }
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog(e.Message);
            }

            try
            {
                var serverState = POIProxySessionManager.getSessionsByUserId(userId);

                foreach (string sessionId in serverState.Keys)
                {
                    
                    //Add connection to the multicast group
                    await Groups.Add(Context.ConnectionId, "session_" + sessionId);

                    //Get the time reference
                    //POIGlobalVar.POIDebugLog("session sync ref: " + sessionId + " " + double.Parse(serverState[sessionId]));

                    var missedEvents = interMsgHandler.getMissedEventsInSession(sessionId, double.Parse(serverState[sessionId]));
                    
                    if(missedEvents.Count != 0)
                    {
                        //Send sync events to the user
                        Clients.Caller.interactiveSessionSynced
                        (
                            sessionId,
                            missedEvents
                        );
                    }
                    
                }
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog("In sync session group information: " + e.Message);
            }
            
        }

        public void unsubscribeSession(string sessionId)
        {
            //Deregister connection id from the session group
            Groups.Remove(Context.ConnectionId, "session_" + sessionId);

            //Remove server state
            POIProxySessionManager.unsubscribeSession(sessionId, Clients.Caller.userId);

            //Release the session token
            POIProxySessionManager.releaseSessionToken(sessionId);
        }

        //Timestamp is the timestamp of the latest event received by the client
        public void syncClientMessageWithSession(string sessionId, double timestamp)
        {
            DataRow record = interMsgHandler.getSessionState(sessionId);
            string sessionState = record["status"] as string;

            //POIGlobalVar.POIDebugLog("In sync timestamp is: " + timestamp);

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

                case "cancelled":
                    Clients.Caller.interactiveSessionCancelled(sessionId);
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
        //public override System.Threading.Tasks.Task OnConnected()
        //{
        //    //Retrieve the user information from the query string
        //    var info = Context.QueryString;

        //    //POIGlobalVar.POIDebugLog(info["service"]);

        //    String service = info["service"];

        //    if (service == null || service == "live")
        //    {
        //        POIGlobalVar.POIDebugLog("Service is null or live");
        //        #region handling live service
        //        POIGlobalVar.POIDebugLog(info["userid"]);
        //        String userId = info["userid"];
        //        POIUser user = null;

        //        if (userId == @"serverLog")
        //        {
        //            Groups.Add(Context.ConnectionId, "serverLog");
        //        }
        //        else
        //        {
        //            //Check if the user exists
        //            if (POIGlobalVar.WebUserProfiles.ContainsKey(userId))
        //            {
        //                //Set the connectionId to user mapping
        //                user = POIGlobalVar.WebUserProfiles[userId];
        //            }
        //            else
        //            {
        //                //Create the user and set the mapping
        //                user = new POIUser(UserType.WEB);
        //                user.UserID = userId;

        //                POIGlobalVar.WebUserProfiles[userId] = user;
        //            }

        //            //Set the connection to user mapping
        //            POIGlobalVar.WebConUserMap[Context.ConnectionId] = user;

        //            //Let the user know the authentication is done
        //            Clients.Caller.handleUserAuthenticated();
        //        }
        //        #endregion
        //    }
        //    else if (service == "interactive")
        //    {
        //        POIGlobalVar.POIDebugLog("Interactive service");

        //        ////Add the connection id to the user group
        //        //Groups.Add(Context.ConnectionId, info["userId"]);

        //        //Add the connection id to the groups
        //        POIGlobalVar.POIDebugLog("user id is : " + info["userId"]);

        //        try
        //        {
        //            var sessions = POIProxySessionManager.getSessionsByUserId(info["userId"]);
        //            foreach (string sessionId in sessions.Keys)
        //            {
        //                POIGlobalVar.POIDebugLog("session list contains: " + sessionId);
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            POIGlobalVar.POIDebugLog(e.Message);
        //        }
                
                

        //        if (info["isReconnect"] == "1")
        //        {
        //            POIGlobalVar.POIDebugLog("Reconnecting from onconnected!");

        //            //Let the new connection enter the broadcast group
        //            if (info["sessions"] != "")
        //            {
        //                List<string> sessionList = jsonHandler.Deserialize<List<string>>(info["sessions"]);
        //                POIGlobalVar.POIDebugLog(info["sessions"]);
        //                POIGlobalVar.POIDebugLog("Total sessions is :" + sessionList.Count);

        //                try
        //                {
        //                    foreach (string sessionId in sessionList)
        //                    {
        //                        Groups.Add(Context.ConnectionId, "session_" + sessionId);
        //                    }
        //                }
        //                catch (Exception e)
        //                {
        //                    POIGlobalVar.POIDebugLog(e.Message);
        //                }
        //            }

        //            //Call reconnect on the client
        //            //Clients.Caller.clientReconnected();
        //        }
        //        else
        //        {
        //            POIGlobalVar.POIDebugLog("Not reconnecting, do nothing!");
        //        }
                
        //    }
        //    else if(service == "log")
        //    {
        //        //For receiving server error log
        //        Groups.Add(Context.ConnectionId, "serverLog");
        //        POIGlobalVar.POIDebugLog("Server log connected!");
        //    }
        //    else
        //    {
        //        POIGlobalVar.POIDebugLog("Service type not recognized");
        //    }

        //    return base.OnConnected();
        //}



        //public override System.Threading.Tasks.Task OnDisconnected()
        //{
        //    var info = Context.QueryString;
        //    String service = info["service"];

        //    if (service == "interactive")
        //    {
        //        try
        //        {
        //            //Handling user reconnecting
        //            POIGlobalVar.POIDebugLog("Client " + info["userId"] + " disconnected");
        //        }
        //        catch (Exception e)
        //        {
        //            POIGlobalVar.POIDebugLog(e.Message);
        //        }

        //    }
            
        //    //Remove the user from the profile
        //    POIUser user = null;
        //    if (POIGlobalVar.WebConUserMap.ContainsKey(Context.ConnectionId))
        //    {
        //        user = POIGlobalVar.WebConUserMap[Context.ConnectionId];
        //    }
      
        //    if (user != null)
        //    {
        //        try
        //        {
        //            //Remove the user from the profiles
        //            POIGlobalVar.WebUserProfiles.Remove(user.UserID);
        //            POIGlobalVar.WebConUserMap.Remove(Context.ConnectionId);
        //        }
        //        catch (Exception e)
        //        {
        //            POIGlobalVar.POIDebugLog(e);
        //        }
        //    }

        //    return base.OnDisconnected();
        //}

        //public override System.Threading.Tasks.Task OnReconnected()
        //{

        //    var info = Context.QueryString;
        //    String service = info["service"];

        //    if (service == "interactive")
        //    {
        //        try
        //        {
        //            //Handling user reconnecting
        //            POIGlobalVar.POIDebugLog("Client " + info["userId"] + " reconnected");
        //        }
        //        catch (Exception e)
        //        {
        //            POIGlobalVar.POIDebugLog(e.Message);
        //        }
                
        //    }

        //    //Notify the client about the reconnection, the client handles the session syncing
        //    Clients.Caller.clientReconnected();

        //    return base.OnReconnected();
        //}

        #endregion

        #region new connection handling function

        //When the client is joining the system for the first time
        public override System.Threading.Tasks.Task OnConnected()
        {
            //Retrieve the user information from the query string
            var info = Context.QueryString;

            String service = info["service"];

            if (service == "interactive")
            {
                POIGlobalVar.POIDebugLog("Client connected to interactive service");
            }
            else if (service == "log")
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
                    //Handling user disconnected
                    POIGlobalVar.POIDebugLog("Client " + info["userId"] + " disconnected");
                }
                catch (Exception e)
                {
                    POIGlobalVar.POIDebugLog(e.Message);
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

            return base.OnReconnected();
        }

        #endregion
    }

   
}
