using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

using POIProxy.Handlers;
using Microsoft.AspNet.SignalR;

namespace POIProxy.Controllers
{
    public class WxToProxy : ApiController
    {
        POIProxyInteractiveMsgHandler interMsgHandler = POIProxyGlobalVar.Kernel.myInterMsgHandler;
        IHubContext hubContext = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();

        // POST api/<controller>
        public void PostMessage([FromBody]string value)
        {
            //Check if post is coming from the allowed IP address

            string userId = "huan";
            string sessionId = "1975";
            string msgType = "text";
            string message = "hello world";
            string mediaId = "dummy";

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
        }

        public void PostControl([FromBody]string value)
        {

        }
    }
}