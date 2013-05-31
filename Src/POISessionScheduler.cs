using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;
using System.Threading;

namespace POIProxy
{
    public class POISessionScheduler
    {
        List<POISessionEvent> highPriorityQueue = new List<POISessionEvent>();
        List<POISessionEvent> lowPriorityQueue = new List<POISessionEvent>();

        public void ScheduleLowPriorityEvent(POISessionEvent sessionEvent)
        {
            lowPriorityQueue.Add(sessionEvent);
        }

        public void ScheduleHighPriorityEvent(POISessionEvent sessionEvent)
        {
            highPriorityQueue.Add(sessionEvent);
        }

        public void Run()
        {
            while (true)
            {
                POISessionEvent nextEvent = null;

                if (highPriorityQueue.Count > 0)
                {
                    nextEvent = highPriorityQueue[0];
                    highPriorityQueue.RemoveAt(0);
                }
                else if (lowPriorityQueue.Count > 0)
                {
                    nextEvent = lowPriorityQueue[0];
                    lowPriorityQueue.RemoveAt(0);
                }

                if (nextEvent != null)
                {
                    nextEvent.Execute();
                }

                //No event available, put the thread to sleep
                if (highPriorityQueue.Count + lowPriorityQueue.Count == 0)
                {
                    Thread.Sleep(1000);
                }
            }
        }
    }

    public abstract class POISessionEvent
    {
        protected POIUser myUser;
        public POIUser User { get { return myUser; } }

        public abstract void Execute();
    }

    public class POISessionPresSendEvent : POISessionEvent
    {
        POIPresentation myPres;

        public POISessionPresSendEvent(POIUser user, POIPresentation pres)
        {
            myUser = user;
            myPres = pres;
        }

        public override void Execute()
        {
            myUser.SendData(myPres.getPacket(), ConType.TCP_DATA);
        }
    }
}
