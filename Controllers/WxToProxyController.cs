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

            switch (msgType)
            {
                case "text":
                    interMsgHandler.textMsgReceived(userId, sessionId, message, POITimestamp.ConvertToUnixTimestamp(DateTime.Now));

                    hubContext.Clients.Group("session_" + sessionId).
                        textMsgReceived(userId, sessionId, message);

                    await POIProxyPushNotifier.textMsgReceived(
                        interMsgHandler.getUsersInSession(sessionId, userId)
                    );

                    
                    break;

                case "image":
                    interMsgHandler.imageMsgReceived(userId, sessionId, mediaId, POITimestamp.ConvertToUnixTimestamp(DateTime.Now));

                    hubContext.Clients.Group("session_" + sessionId).
                        imageMsgReceived(userId, sessionId, mediaId);

                    await POIProxyPushNotifier.imageMsgReceived(
                        interMsgHandler.getUsersInSession(sessionId, userId)
                    );

                    
                    break;

                case "voice":
                    interMsgHandler.voiceMsgReceived(userId, sessionId, mediaId, POITimestamp.ConvertToUnixTimestamp(DateTime.Now));

                    hubContext.Clients.Group("session_" + sessionId).
                        voiceMsgReceived(userId, sessionId, mediaId);

                    await POIProxyPushNotifier.voiceMsgReceived(
                        interMsgHandler.getUsersInSession(sessionId, userId)
                    );

                    
                    break;

                case "illustration":
                    interMsgHandler.illustrationMsgReceived(userId, sessionId, mediaId, POITimestamp.ConvertToUnixTimestamp(DateTime.Now));

                    hubContext.Clients.Group("session_" + sessionId).
                        illustrationMsgReceived(userId, sessionId, mediaId);

                    await POIProxyPushNotifier.illustrationMsgReceived(
                        interMsgHandler.getUsersInSession(sessionId, userId)
                    );

                    
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
            string sessionId = msgInfo["sessionId"];
            string userId, infoStr, desc, mediaId; 
            int rating;

            switch (type)
            {
                case "sessionCreated":
                    //Initialize the session archive
                    userId = msgInfo["userId"];
                    infoStr = msgInfo["info"];
                    interMsgHandler.initSessionArchive(userId, sessionId, 
                        jsonHandler.Deserialize<Dictionary<string,string>>(infoStr));
                    break;

                case "ratingReceived":
                    //Update the database
                    userId = msgInfo["userId"];
                    rating = Convert.ToInt32(msgInfo["rating"]);
                    await interMsgHandler.rateInteractiveSession(userId, sessionId, rating);

                    //Send notification to all clients in the session
                    hubContext.Clients.Group("session_" + sessionId)
                        .interactiveSessionRatedAndEnded(sessionId, rating);
                    break;

                case "sessionUpdated":
                    desc = msgInfo["description"];
                    mediaId = msgInfo["mediaId"];

                    if (desc != "") interMsgHandler.updateQuestionDescription(sessionId, desc);
                    if (mediaId != "") interMsgHandler.updateQuestionMediaId(sessionId, mediaId);

                    break;

                case "sessionCancelled":
                    POIGlobalVar.POIDebugLog("Here" + " " + sessionId);
                    //userId = msgInfo["userId"];
                    interMsgHandler.cancelInteractiveSession("", sessionId);

                    break;

                case "sessionJoined":
                    //Do not handle the session join event for now
                    break;
            }
        }
    }
}