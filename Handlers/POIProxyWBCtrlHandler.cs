using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;
//using SignalR;
//using SignalR.Hosting.Self;
using Microsoft.AspNet.SignalR;
using System.Web.Script.Serialization;

namespace POIProxy.Handlers
{
    public class POIProxyWBCtrlHandler : POIWhiteBoardMsgCB, POICommentCB
    {

        public void showWhiteBoard() { }
        public void hideWhiteBoard() { }


        public void whiteboardCtrlMsgReceived(POIWhiteboardMsg msg, POIUser myUser)
        {
            //Broadcast the event
            POISessionManager manager = POIProxyGlobalVar.Kernel.mySessionManager;
            manager.broadcastMessageToViewers(myUser, msg);

            //Log the event
            try
            {
                var session = manager.Registery.GetSessionByUser(myUser);
                session.MdArchive.LogEvent(msg);
            }
            catch (Exception e)
            {

            }
            
        }

        public void handleComment(POIComment msg, POIUser myUser)
        {
            //Broadcast the event
            POISessionManager manager = POIProxyGlobalVar.Kernel.mySessionManager;
            manager.broadcastMessageToViewers(myUser, msg);

            //Check if the comment contains audio comment
            //If so, upload the comment to the content server



            //Log the event
            try
            {
                var session = manager.Registery.GetSessionByUser(myUser);
                session.MdArchive.LogEvent(msg);
            }
            catch (Exception e)
            {

            }
            
        }

        //Handle comment in the format of a json string
        public void handleStringComment(string cmntString, POIUser webUser)
        {
            JavaScriptSerializer jsHandler = new JavaScriptSerializer();
            POIComment comment = jsHandler.Deserialize(cmntString, typeof(POIComment)) as POIComment;

            comment.calculateSize();

            POISessionManager manager = POIProxyGlobalVar.Kernel.mySessionManager;
            manager.sendMessageToCommanders(webUser, comment);

        }
    }
}
