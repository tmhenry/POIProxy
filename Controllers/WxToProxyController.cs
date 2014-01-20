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

namespace POIProxy.Controllers
{

    public class WxToProxyController : ApiController
    {
        POIProxyInteractiveMsgHandler interMsgHandler = POIProxyGlobalVar.Kernel.myInterMsgHandler;
        IHubContext hubContext = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
        JavaScriptSerializer jsonHandler = new JavaScriptSerializer();

        [HttpPost]
        public string Message(HttpRequestMessage request)
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
                    interMsgHandler.textMsgReceived(userId, sessionId, message);
                    hubContext.Clients.Group("session_" + sessionId).
                        textMsgReceived(userId, sessionId, message);
                    break;

                case "image":
                    interMsgHandler.imageMsgReceived(userId, sessionId, mediaId);
                    hubContext.Clients.Group("session_" + sessionId).
                        imageMsgReceived(userId, sessionId, mediaId);
                    break;

                case "voice":
                    interMsgHandler.voiceMsgReceived(userId, sessionId, mediaId);
                    hubContext.Clients.Group("session_" + sessionId).
                        voiceMsgReceived(userId, sessionId, mediaId);
                    break;

                case "illustration":
                    interMsgHandler.illustrationMsgReceived(userId, sessionId, mediaId);
                    hubContext.Clients.Group("session_" + sessionId).
                        illustrationMsgReceived(userId, sessionId, mediaId);
                    break;
            }

            return "{Message:OK}";
        }

        [HttpPost]
        public void Session(HttpRequestMessage request)
        {
            string content = request.Content.ReadAsStringAsync().Result;
            Dictionary<string, string> msgInfo = jsonHandler.Deserialize<Dictionary<string, string>>(content);

            string type = msgInfo["type"];
            string userId = msgInfo["userId"];
            string sessionId = msgInfo["sessionId"];

            switch (type)
            {
                case "sessionCreated":
                    //Initialize the session archive
                    interMsgHandler.initSessionArchive(userId, sessionId);
                    break;

                case "sessionJoined":
                    //Do not handle the session join event for now
                    break;
            }
        }
    }
}