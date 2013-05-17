using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;
using System.Threading;

using SignalR;
using SignalR.Hubs;
using POIProxy.SignalRFun;

using System.IO;

namespace POIProxy
{
    public class POISession
    {
        
        private List<POIUser> commanders;
        private List<POIUser> viewers;
        private int id;

        private POISessionPresController presController;
        private POISessionScheduler myScheduler = new POISessionScheduler();

        private POIMetadataArchive mdArchive;

        public List<POIUser> Commanders { get { return commanders; } }
        public List<POIUser> Viewers { get { return viewers; } }
        public int Id { get { return id; } }
        public POISessionPresController PresController { get { return presController; } }
        public POISessionInfo Info { get; set; }

        public POIMetadataArchive MdArchive { get { return mdArchive; } }

        public POISession(POIUser commander, int sessionId, int contentId)
        {
            //Load the presentation content according to the contentId
            POIPresentation presContent = POIPresentation.LoadPresFromContentServer(contentId);

            //Create the archive for metadata
            mdArchive = new POIMetadataArchive(contentId, sessionId);
            
            //POIPresentation presContent = new POIPresentation();
            //presContent.LoadPresentationFromStorage();

            presController = new POISessionPresController(this);
            presController.LoadPresentation(presContent);

            Info = new POISessionInfo();
            Info.organization = @"Pipe of Insight";
            Info.presenterName = commander.UserID;
            Info.sessionId = sessionId;
            Info.contentId = contentId;

            id = sessionId;

            commanders = new List<POIUser>();
            viewers = new List<POIUser>();
            JoinAsCommander(commander);


            Thread schedulerThread = new Thread(StartScheduler);
            schedulerThread.Name = @"SessionScheduler_" + id;
            schedulerThread.Start();
        }

        public void StartScheduler()
        {
            myScheduler.Run();
        }

        public void JoinAsViewer(POIUser user)
        {
            if (!viewers.Contains(user))
            {
                viewers.Add(user);

                SendInitialSlides(user);
            }
        }

        public void JoinAsCommander(POIUser user)
        {
            if (!commanders.Contains(user))
            {
                commanders.Add(user);
                viewers.Add(user);

                SendInitialSlides(user);
            }
        }

        public void SendInitialSlides(POIUser user)
        {
            List<POISlide> myList = presController.GetInitialSlides();

            POIPresentation pres = presController.getPresMsgTemplate();
            for (int i = 0; i < myList.Count; i++)
            {
                pres.Insert(myList[i]);
            }

            myScheduler.ScheduleLowPriorityEvent
            (
                new POISessionPresSendEvent(user, pres)
            );
        }

        public void BroadcastPreloadSlide()
        {
            POIPresentation pres = presController.getPresMsgTemplate();
            POISlide slide = presController.GetPreloadSlide();

            if (slide != null)
            {
                pres.Insert(slide);

                for (int i = 0; i < viewers.Count; i++)
                {
                    myScheduler.ScheduleHighPriorityEvent
                    (
                        new POISessionPresSendEvent(viewers[i], pres)
                    );
                }

                //Broadcast the slide to all the web clients
                //var context = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
                //context.Clients[id.ToString()].getSlide(slide.Index);
            }
            else
            {
                Console.WriteLine("Preload slide is null!");
            }
        }

        public void LeaveAsViewer(POIUser user)
        {
            if (viewers.Contains(user))
                viewers.Remove(user);
        }

        public void LeaveAsCommander(POIUser user)
        {
            if (commanders.Contains(user))
                commanders.Remove(user);
        }

        public bool IsCommander(POIUser user)
        {
            return commanders.Contains(user);
        }

        public bool IsViewer(POIUser user)
        {
            return viewers.Contains(user);
        }

        public void SessionEnd()
        {
            mdArchive.WriteArchive();
            Dictionary<string, string> reqDict = new Dictionary<string, string>();
            reqDict["sessionId"] = id.ToString();
            POIWebService.EndSession(reqDict);
        }
    }

    public class POISessionInfo
    {
        public string presenterName;
        public string organization;
        public int sessionId;
        public int contentId;
    }
}
