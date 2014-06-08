using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//using SignalR.Hubs;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Owin;

using System.Web.Script.Serialization;

using System.Threading.Tasks;
using System.IO;
using System.Data;

namespace POIProxy
{
    [HubName("poiProxy")]
    public class POIProxyHub : Hub
    {
        static POIProxyInteractiveMsgHandler interMsgHandler = POIGlobalVar.Kernel.myInterMsgHandler;
        JavaScriptSerializer jsonHandler = new JavaScriptSerializer();

        public async Task textMsgReceived(string sessionId, string message, double timestamp)
        {
            if (!POIProxySessionManager.checkEventExists(sessionId, timestamp))
            {
                interMsgHandler.textMsgReceived(Clients.Caller.userId, sessionId, message, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    textMsgReceived(Clients.Caller.userId, sessionId, message, timestamp);

                Clients.Caller.msgAckReceived(sessionId, timestamp);
                PPLog.infoLog("[POIProxyHub textMsgReceived] " + message + " session:" + sessionId + " timestamp: " + timestamp);

                //Notify the weixin server
                await POIProxyToWxApi.textMsgReceived(Clients.Caller.userId, sessionId, message);

                //Send push notification
                await POIProxyPushNotifier.textMsgReceived(sessionId);
            }
            else
            {
                PPLog.infoLog("[POIProxyHub textMsgReceived] Message duplicate: timestamp is " + timestamp);
                //Message duplicate: send back the ack
                Clients.Caller.msgAckReceived(sessionId, timestamp);
            }
            
        }

        public async Task imageMsgReceived(string sessionId, string mediaId, double timestamp)
        {
            if (!POIProxySessionManager.checkEventExists(sessionId, timestamp))
            {
                interMsgHandler.imageMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    imageMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Caller.msgAckReceived(sessionId, timestamp);
                PPLog.infoLog("[POIProxyHub imageMsgReceived] session: " + sessionId + " timestamp: " + timestamp);

                await POIProxyToWxApi.imageMsgReceived(Clients.Caller.userId, sessionId, mediaId);

                //Send push notification
                await POIProxyPushNotifier.imageMsgReceived(sessionId);
            }
            else
            {
                PPLog.infoLog("[POIProxyHub imageMsgReceived] Message duplicate: timestamp is " + timestamp);
                //Message duplicate: send back the ack
                Clients.Caller.msgAckReceived(sessionId, timestamp);
            }
        }

        public async Task voiceMsgReceived(string sessionId, string mediaId, double timestamp)
        {
            if (!POIProxySessionManager.checkEventExists(sessionId, timestamp))
            {
                interMsgHandler.voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Caller.msgAckReceived(sessionId, timestamp);
                PPLog.infoLog("[POIProxyHub voiceMsgReceived] session: " + sessionId + " timestamp: " + timestamp);

                await POIProxyToWxApi.voiceMsgReceived(Clients.Caller.userId, sessionId, mediaId);

                //Send push notification
                await POIProxyPushNotifier.voiceMsgReceived(sessionId);
            }
            else
            {
                PPLog.infoLog("[POIProxyHub voiceMsgReceived] Message duplicate: timestamp is " + timestamp);
                //Message duplicate: send back the ack
                Clients.Caller.msgAckReceived(sessionId, timestamp);
            }
        }

        public async Task illustrationMsgReceived(string sessionId, string mediaId, double timestamp)
        {
            if (!POIProxySessionManager.checkEventExists(sessionId, timestamp))
            {
                interMsgHandler.illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Group("session_" + sessionId, Context.ConnectionId).
                    illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId, timestamp);

                Clients.Caller.msgAckReceived(sessionId, timestamp);
                PPLog.infoLog("[POIProxyHub illustrationMsgReceived] session: " + sessionId + " timestamp: " + timestamp);

                await POIProxyToWxApi.illustrationMsgReceived(Clients.Caller.userId, sessionId, mediaId);

                //Send push notification
                await POIProxyPushNotifier.illustrationMsgReceived(sessionId);
            }
            else
            {
                PPLog.infoLog("[POIProxyHub illustrationMsgReceived] Message duplicate: timestamp is " + timestamp);
                //Message duplicate: send back the ack
                Clients.Caller.msgAckReceived(sessionId, timestamp);
            }
        }

        public async Task createInteractiveSession(string mediaId, string description)
        {
            string accessType = "group";
            
            //Create the session
            Tuple<string,string> result = interMsgHandler.
                createInteractiveSession(Clients.Caller.userId, mediaId, description, accessType);
            string presId = result.Item1;
            string sessionId = result.Item2;

            PPLog.infoLog("[POIProxyHub createInteractiveSession] description: " + description + "sessionId: " + sessionId);

            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            await Groups.Add(Context.ConnectionId, "session_" + sessionId);

            //Notify the user the connection has been created
            Clients.Caller.interactiveSessionCreated(presId, sessionId, timestamp);

            //Make the session open after everything is ready
            interMsgHandler.updateSessionStatus(sessionId, "open");

            await POIProxyPushNotifier.sessionCreated(sessionId);
        }

        public async Task joinInteractiveSession(string sessionId)
        {
            var archiveInfo = POIProxySessionManager.getSessionInfo(sessionId);            

            if (double.Parse(archiveInfo["create_at"])
                >= POITimestamp.ConvertToUnixTimestamp(DateTime.Now.AddSeconds(-60)))
            {
                PPLog.infoLog("[POIProxyHub joinInteractiveSession] Cannot join the session, not passing time limit");
                Clients.Caller.interactiveSessionJoinBeforeStarted(sessionId);
            }
            else if (POIProxySessionManager.checkUserInSession(sessionId, Clients.Caller.userId))
            {
                //User already in the session
                PPLog.infoLog("[POIProxyHub joinInteractiveSession] Session already joined");

                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                string archiveJson = jsonHandler.Serialize(POIProxySessionManager.getSessionArchive(sessionId));

                await Groups.Add(Context.ConnectionId, "session_" + sessionId);

                PPLog.infoLog(archiveJson);

                //Send the archive to the user
                Clients.Caller.interactiveSessionJoined(sessionId, archiveJson, timestamp);
            }
            else if (POIProxySessionManager.acquireSessionToken(sessionId))
            {
                
                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

                interMsgHandler.joinInteractiveSession(Clients.Caller.userId, sessionId, timestamp);
                //var userInfo = interMsgHandler.getUserInfoById(Clients.Caller.userId);
                var userInfo = POIProxySessionManager.getUserInfo(Clients.Caller.userId);

                string archiveJson = jsonHandler.Serialize(POIProxySessionManager.getSessionArchive(sessionId));
                string userInfoJson = jsonHandler.Serialize(userInfo);

                await Groups.Add(Context.ConnectionId, "session_" + sessionId);

                PPLog.infoLog("[POIProxyHub joinInteractiveSession] Session is open, joined! archive: " + archiveJson);

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
                PPLog.infoLog("[POIProxyHub joinInteractiveSession] Cannot join the session, taken by others");
                Clients.Caller.interactiveSessionJoinFailed(sessionId);
            }
        }

        public async Task endInteractiveSession(string sessionId)
        {
            //Update the database
            interMsgHandler.endInteractiveSession(Clients.Caller.userId, sessionId);

            //Send notification to all clients in the session
            Clients.Group("session_" + sessionId, Context.ConnectionId)
                .interactiveSessionEnded(Clients.Caller.userId, sessionId);

            //Notify the weixin server about the ending operation
            await POIProxyToWxApi.interactiveSessionEnded(Clients.Caller.userId, sessionId);
            PPLog.infoLog("[POIProxyHub endInteractiveSession] sessionId: " + sessionId + " userid:" + Clients.Caller.userId);

            //Send push notification
            await POIProxyPushNotifier.sessionEnded(sessionId);
        }

        public async Task rateAndEndInteractiveSession(string sessionId, int rating)
        {
            //Update the database
            interMsgHandler.rateInteractiveSession(Clients.Caller.userId, sessionId, rating);

            //Send notification to all clients in the session
            Clients.Group("session_" + sessionId, Context.ConnectionId)
                .interactiveSessionRatedAndEnded(Clients.Caller.userId, sessionId, rating);
            PPLog.infoLog("[POIProxyHub rateAndEndInteractiveSession] session: " + sessionId + " userid:" + Clients.Caller.userId);

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
            PPLog.infoLog("[POIProxyHub reraiseInteractiveSession] session: " + sessionId + " newSessionId: " + newSessionId);

            //Notify the weixin users about the cancel operation
            await POIProxyToWxApi.interactiveSessionCancelled(Clients.Caller.userId, sessionId);

            //Make the session open after everything is ready
            interMsgHandler.updateSessionStatus(newSessionId, "open");

            await POIProxyPushNotifier.sessionCreated(newSessionId);
        }

        public async Task syncClient(string sessionListJson)
        {
            string userId = Clients.Caller.userId;
            PPLog.infoLog("[POIProxyHub syncClient] userId: " + userId + " session list from client is :" + sessionListJson);

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
                PPLog.errorLog(e.Message);
            }

            try
            {
                var serverState = POIProxySessionManager.getSessionsByUserId(userId);

                foreach (string sessionId in serverState.Keys)
                {
                    
                    //Add connection to the multicast group
                    await Groups.Add(Context.ConnectionId, "session_" + sessionId);

                    //Get the time reference
                    //PPLog.infoLog("session sync ref: " + sessionId + " " + double.Parse(serverState[sessionId]));

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
                PPLog.errorLog("In sync session group information: " + e.Message);
            }
            
        }

        public void unsubscribeSession(string sessionId)
        {
            //Deregister connection id from the session group
            Groups.Remove(Context.ConnectionId, "session_" + sessionId);

            //Remove server state
            POIProxySessionManager.unsubscribeSession(sessionId, Clients.Caller.userId);
            PPLog.infoLog("[POIProxyHub unsubscribeSession] sessionId: " + sessionId + " userid:" + Clients.Caller.userId);
        }

        #region new connection handling function

        //When the client is joining the system for the first time
        public override System.Threading.Tasks.Task OnConnected()
        {
            //Retrieve the user information from the query string
            var info = Context.QueryString;

            String service = info["service"];

            if (service == "interactive")
            {
                PPLog.infoLog("[POIProxyHub OnConnected] Client connected to interactive service");
            }
            else if (service == "log")
            {
                //For receiving server error log
                Groups.Add(Context.ConnectionId, "serverLog");
                PPLog.infoLog("[POIProxyHub OnConnected] Server log connected!");
            }
            else
            {
                PPLog.infoLog("[POIProxyHub OnConnected] Service type not recognized");
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
                    PPLog.infoLog("[POIProxyHub OnDisconnected] Client " + info["userId"] + " disconnected");
                }
                catch (Exception e)
                {
                    PPLog.errorLog(e.Message);
                }

            }

            return base.OnDisconnected();
        }

        public override System.Threading.Tasks.Task OnReconnected()
        {
            //var info = Context.QueryString;
            try
            {
                //Handling user reconnecting
                //PPLog.infoLog("[POIProxyHub OnReconnected] Client " + info["userId"] + " reconnected");
                PPLog.infoLog("[POIProxyHub OnReconnected] Client reconnected");
            }
            catch (Exception e)
            {
                PPLog.errorLog(e.Message);
            }

            /*if (service == "interactive")
            {
                
                String service = info["service"];

            }*/

            return base.OnReconnected();
        }

        #endregion
    }

   
}
