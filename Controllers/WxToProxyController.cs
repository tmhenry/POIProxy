using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

using POIProxy.Handlers;
using Microsoft.AspNet.SignalR;

using POILibCommunication;
using System.Web.Script.Serialization;
using System.Threading.Tasks;

namespace POIProxy.Controllers
{

    public class WxToProxyController : ApiController
    {
        POIProxyInteractiveMsgHandler interMsgHandler = POIProxyGlobalVar.Kernel.myInterMsgHandler;
        IHubContext hubContext = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
        JavaScriptSerializer jsonHandler = new JavaScriptSerializer();

        [HttpPost]
        public async Task<string> Message(HttpRequestMessage request)
        {
            //Check if post is coming from the allowed IP address
            string content = request.Content.ReadAsStringAsync().Result;
            Dictionary<string, string> msgInfo = jsonHandler.Deserialize<Dictionary<string, string>>(content);

            string userId = msgInfo["userId"];
            string sessionId = msgInfo["sessionId"];
            string msgType = msgInfo["msgType"];
            string message = msgInfo["message"];
            string mediaId = msgInfo["mediaId"];
            double timestamp = double.Parse(msgInfo["timestamp"]);

            POIGlobalVar.POIDebugLog("Message timestamp: " + timestamp);

            switch (msgType)
            {
                case "text":
                    interMsgHandler.textMsgReceived(userId, sessionId, message, timestamp);

                    hubContext.Clients.Group("session_" + sessionId).
                        textMsgReceived(userId, sessionId, message, timestamp);

                    await POIProxyPushNotifier.textMsgReceived(sessionId);
                    
                    break;

                case "image":
                    interMsgHandler.imageMsgReceived(userId, sessionId, mediaId, timestamp);

                    hubContext.Clients.Group("session_" + sessionId).
                        imageMsgReceived(userId, sessionId, mediaId, timestamp);

                    await POIProxyPushNotifier.imageMsgReceived(sessionId);
                    
                    break;

                case "voice":
                    interMsgHandler.voiceMsgReceived(userId, sessionId, mediaId, timestamp);

                    hubContext.Clients.Group("session_" + sessionId).
                        voiceMsgReceived(userId, sessionId, mediaId, timestamp);

                    await POIProxyPushNotifier.voiceMsgReceived(sessionId);

                    
                    break;

                case "illustration":
                    interMsgHandler.illustrationMsgReceived(userId, sessionId, mediaId, timestamp);

                    hubContext.Clients.Group("session_" + sessionId).
                        illustrationMsgReceived(userId, sessionId, mediaId, timestamp);

                    await POIProxyPushNotifier.illustrationMsgReceived(sessionId);

                    break;
            }

            return "{Message:OK}";
        }

        [HttpPost]
        public async Task Session(HttpRequestMessage request)
        {
            string content = request.Content.ReadAsStringAsync().Result;
            Dictionary<string, string> msgInfo = jsonHandler.Deserialize<Dictionary<string, string>>(content);

            string type = msgInfo["type"];
            string sessionId, userId, infoStr, desc, mediaId, userInfo, newSessionId; 
            int rating;

            POIGlobalVar.POIDebugLog("Wx to proxy session post type is: " + type);

            try
            {
                switch (type)
                {
                    case "sessionCreated":
                        //Initialize the session archive
                        sessionId = msgInfo["sessionId"];
                        userId = msgInfo["userId"];
                        infoStr = msgInfo["info"];
                        interMsgHandler.initSessionArchive(jsonHandler.Deserialize<Dictionary<string, string>>(infoStr));

                        await POIProxyPushNotifier.sessionCreated(sessionId);

                        break;

                    case "ratingReceived":
                        //Update the database
                        sessionId = msgInfo["sessionId"];
                        userId = msgInfo["userId"];
                        rating = Convert.ToInt32(msgInfo["rating"]);
                        interMsgHandler.rateInteractiveSession(userId, sessionId, rating);

                        //Send notification to all clients in the session
                        hubContext.Clients.Group("session_" + sessionId)
                            .interactiveSessionRatedAndEnded(userId, sessionId, rating);

                        await POIProxyPushNotifier.sessionRated(sessionId, rating);

                        break;

                    case "sessionUpdated":
                        sessionId = msgInfo["sessionId"];
                        desc = msgInfo["description"];
                        mediaId = msgInfo["mediaId"];

                        if (desc != "") interMsgHandler.updateQuestionDescription(sessionId, desc);
                        if (mediaId != "") interMsgHandler.updateQuestionMediaId(sessionId, mediaId);

                        break;

                    case "sessionCancelled":
                        sessionId = msgInfo["sessionId"];
                        POIGlobalVar.POIDebugLog("Here" + " " + sessionId);
                        //userId = msgInfo["userId"];
                        interMsgHandler.cancelInteractiveSession("", sessionId);

                        break;

                    case "sessionJoined":
                        sessionId = msgInfo["sessionId"];
                        POIGlobalVar.POIDebugLog("Session joined: " + sessionId);
                        userId = msgInfo["userId"];
                        userInfo = msgInfo["userInfo"];
                        POIGlobalVar.POIDebugLog("user id is: " + userId);
                        interMsgHandler.archiveSessionJoinedEvent(userId, sessionId);

                        hubContext.Clients.Group("session_" + sessionId)
                            .interactiveSessionNewUserJoined(userId, sessionId, userInfo, POITimestamp.ConvertToUnixTimestamp(DateTime.Now));

                        await POIProxyPushNotifier.sessionJoined(sessionId);

                        break;

                    case "sessionEnded":
                        sessionId = msgInfo["sessionId"];
                        POIGlobalVar.POIDebugLog("Session ended: " + sessionId);
                        userId = msgInfo["userId"];
                        interMsgHandler.endInteractiveSession(userId, sessionId);

                        hubContext.Clients.Group("session_" + sessionId)
                            .interactiveSessionEnded(userId, sessionId);

                        await POIProxyPushNotifier.sessionEnded(sessionId);

                        break;

                    //case "sessionReraised":
                    //    sessionId = msgInfo["sessionId"];
                    //    POIGlobalVar.POIDebugLog("Session reraised " + sessionId);
                    //    newSessionId = msgInfo["newSessionId"];
                    //    userId = msgInfo["userId"];
                    //    POIGlobalVar.POIDebugLog("New session is " + newSessionId);

                    //    interMsgHandler.reraiseInteractiveSession(userId, sessionId, newSessionId);

                    //    hubContext.Clients.Group("session_" + sessionId)
                    //        .textMsgReceived(userId, sessionId, "志愿者你好，同学已经取消了这次提问，取消的提问不会影响你的积分",
                    //        POITimestamp.ConvertToUnixTimestamp(DateTime.Now));

                    //    //Notify the tutor about the cancelling operation
                    //    hubContext.Clients.Group("session_" + sessionId)
                    //        .interactiveSessionCancelled(sessionId);

                    //    //Make the session open after everything is ready
                    //    interMsgHandler.updateSessionStatus(newSessionId, "open");

                    //    await POIProxyPushNotifier.sessionCreated(newSessionId);

                    //    break;

                    //Join and create operation received from weixin 
                    case "joinSession":
                        sessionId = msgInfo["sessionId"];
                        userId = msgInfo["userId"];
                        await wxJoinInteractiveSession(userId, sessionId);

                        break;

                    case "createSession":
                        userId = msgInfo["userId"];
                        mediaId = msgInfo["mediaId"];
                        desc = msgInfo["description"];
                        await wxCreateInteractiveSession(userId, mediaId, desc);

                        break;

                    case "reraiseSession":
                        sessionId = msgInfo["sessionId"];
                        userId = msgInfo["userId"];
                        await wxReraiseInteractiveSession(userId, sessionId);

                        break;
                }
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog("In wx to proxy post session: " + e.Message);
            }
            
        }



        private async Task wxCreateInteractiveSession(string userId, string mediaId, string description)
        {
            //Create the session
            Tuple<string, string> result = interMsgHandler.
                createInteractiveSession(userId, mediaId, description);
            string presId = result.Item1;
            string sessionId = result.Item2;

            POIGlobalVar.POIDebugLog("Description is " + description);

            POIGlobalVar.POIDebugLog("Session created!: " + sessionId);

            //Notify the weixin user the connection has been created
            await POIProxyToWxApi.interactiveSessionCreated(userId, sessionId);

            //Make the session open after everything is ready
            interMsgHandler.updateSessionStatus(sessionId, "open");

            await POIProxyPushNotifier.sessionCreated(sessionId);
        }

        private async Task wxJoinInteractiveSession(string userId, string sessionId)
        {
            POIInteractiveSessionArchive archive = interMsgHandler.getArchiveBySessionId(sessionId);

            if (double.Parse(archive.Info["create_at"])
                < POITimestamp.ConvertToUnixTimestamp(DateTime.Now.AddSeconds(-60)))
            {

                if (POIProxySessionManager.acquireSessionToken(sessionId, 10))
                {
                    POIGlobalVar.POIDebugLog("Session is open, joined!");
                    double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

                    interMsgHandler.joinInteractiveSession(userId, sessionId, timestamp);
                    //var userInfo = interMsgHandler.getUserInfoById(Clients.Caller.userId);
                    var userInfo = POIProxySessionManager.getUserInfo(userId);

                    string archiveJson = jsonHandler.Serialize(archive);
                    string userInfoJson = jsonHandler.Serialize(userInfo);

                    POIProxySessionManager.subscribeSession(sessionId, userId);

                    POIGlobalVar.POIDebugLog(archiveJson);

                    //Notify the weixin users about the join operation
                    await POIProxyToWxApi.interactiveSessionNewUserJoined(userId, sessionId, userInfoJson);

                    hubContext.Clients.Group("session_" + sessionId).
                        interactiveSessionNewUserJoined(userId, sessionId, userInfoJson, timestamp);

                    //Notify the wexin server about the join operation
                    await POIProxyToWxApi.interactiveSessionNewUserJoined(userId, sessionId, userInfoJson);

                    //Send push notification
                    await POIProxyPushNotifier.sessionJoined(sessionId);
                }
                else
                {
                    POIGlobalVar.POIDebugLog("Cannot join the session, taken by others");
                    //Notify the weixin user about the join failed
                    await POIProxyToWxApi.interactiveSessionJoinFailed(userId, sessionId);
                }
            }
            else
            {
                POIGlobalVar.POIDebugLog("Cannot join the session, not passing time limit");
                //Notify the weixin user about the join failed
                await POIProxyToWxApi.interactiveSessionJoinBeforeTimeLimit(userId, sessionId);
            }
        }

        private async Task wxReraiseInteractiveSession(string userId, string sessionId)
        {
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            string newSessionId = interMsgHandler.duplicateInteractiveSession(sessionId, timestamp);
            await interMsgHandler.reraiseInteractiveSession(userId, sessionId, newSessionId, timestamp);

            //Notify the student about interactive session reraised
            await POIProxyToWxApi.interactiveSessionReraised(userId, sessionId, newSessionId);

            hubContext.Clients.Group("session_" + sessionId).
                textMsgReceived(userId, sessionId,
                "志愿者你好，同学已经取消了这次提问，取消的提问不会影响你的积分", timestamp);

            //Notify the signalr users about the cancel operation
            hubContext.Clients.Group("session_" + sessionId).
                interactiveSessionCancelled(sessionId);

            //Notify the weixin users about the cancel operation
            await POIProxyToWxApi.interactiveSessionCancelled(userId, sessionId);

            //Make the session open after everything is ready
            interMsgHandler.updateSessionStatus(newSessionId, "open");

            await POIProxyPushNotifier.sessionCreated(newSessionId);
        }
    }
}