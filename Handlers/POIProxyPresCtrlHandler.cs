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
    public class POIProxyPresCtrlHandler : POIPresentationControlMsgCB
    {

        public void presCtrlMsgReceived(POIPresCtrlMsg msg, POIUser myUser)
        {
            //Broadcast the event
            POISessionManager manager = POIProxyGlobalVar.Kernel.mySessionManager;
            manager.broadcastMessageToViewers(myUser, msg);

            //Handle the msg locally
            try
            {
                var session = manager.Registery.GetSessionByUser(myUser);
                HandlePresCtrlMsgByProxy(session, msg);
            }
            catch(Exception e)
            {

            }
            
        }

        private void HandlePresCtrlMsgByProxy(POISession session, POIPresCtrlMsg msg)
        {
            var presController = session.PresController;
            
            switch((PresCtrlType)msg.CtrlType)
            {
                case PresCtrlType.Next:
                    msg.SlideIndex = presController.CurSlideIndex;
                    presController.playNext();

                    //Update the indexer only if next slide is loaded
                    if (presController.CurSlideIndex > msg.SlideIndex)
                    {
                        session.MdArchive.LogEventAndUpdateEventIndexer(msg);
                    }
                    else
                    {
                        session.MdArchive.LogEvent(msg);
                    }
                    
                    break;

                case PresCtrlType.Prev:
                    msg.SlideIndex = presController.CurSlideIndex;
                    presController.playPrev();
                    session.MdArchive.LogEvent(msg);
                    
                    break;

                case PresCtrlType.Jump:
                    session.MdArchive.LogEventAndUpdateEventIndexer(msg);
                    presController.JumpToSlide(msg.SlideIndex);
                    
                    break;

                default:
                    POIGlobalVar.POIDebugLog("Presentation ctrl type not recognized");
                    break;
            }
        }
    }
}
