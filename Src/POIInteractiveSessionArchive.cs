using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using POILibCommunication;

namespace POIProxy
{
    public class POIInteractiveSessionArchive
    {
        public string SessionId { get; set; }
        public Dictionary<string, string> Info { get; set; }
        public List<string> UserList { get; set; }
        
        public List<POIInteractiveEvent> EventList { 
            get 
            {
                return POIProxySessionManager.getSessionEventList(SessionId);
            }
        }

        //For checking duplicate messages
        //private List<double> EventTimestamps { get; set; }

        //For checking session state
        private readonly object statusLock = new object();
        private string Status { get; set; }

        public POIInteractiveSessionArchive(Dictionary<string, string> info)
        {
            SessionId = info["session_id"];
            Info = info;

            UserList = new List<string>();
            //EventList = new List<POIInteractiveEvent>();
            //EventTimestamps = new List<double>();

            //Update the archive status
            lock (statusLock)
            {
                Status = info["status"];
            }

            //Add the users to the session user list and archive the correponding events
            if (info["student_id"] != null)
            {
                addUserToUserList(info["student_id"]);
                archiveSessionCreatedEvent(info["student_id"], double.Parse(info["create_at"]));
            }

            if (info["tutor_id"] != null)
            {
                addUserToUserList(info["tutor_id"]);
                archiveSessionJoinedEvent(info["tutor_id"], double.Parse(info["start_at"]));
            }
        }

        public int joinSessionIfOpen()
        {
            lock (statusLock)
            {
                if (Status == "open")
                {
                    double createTime = double.Parse(Info["create_at"]);
                    POIGlobalVar.POIDebugLog("In join, create time is a " + createTime + 
                        " and threshold is" + POITimestamp.ConvertToUnixTimestamp(DateTime.Now.AddSeconds(-60)));
                    if (createTime < POITimestamp.ConvertToUnixTimestamp(DateTime.Now.AddSeconds(-60)))
                    {
                        POIGlobalVar.POIDebugLog("Session is open!");
                        Status = "serving";
                        return 0;
                    }
                    else
                    {
                        POIGlobalVar.POIDebugLog("Session is counting for open!");
                        return 1;
                    }
                }
                else
                {
                    POIGlobalVar.POIDebugLog("In join, session is not open");
                    return 2;
                }
            }
        }

        public void updateSessionStatusServing()
        {
            lock (statusLock)
            {
                Status = "serving";
            }
        }

        public void addUserToUserList(string userId)
        {
            UserList.Add(userId);
        }

        public bool checkEventExists(double eventTimestamp)
        {
            //return EventTimestamps.Contains(eventTimestamp);
            return POIProxySessionManager.checkEventExists(SessionId, eventTimestamp);
        }

        public void archiveTextEvent(string userId, string message, double timestamp)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                EventIndex = EventList.Count,
                EventType = "text",
                MediaId = "",
                UserId = userId,
                Timestamp = timestamp,
                Message = message
            };

            //POIGlobalVar.POIDebugLog("In archive text, event array count is " + EventList.Count);

            //EventList.Add(poiEvent);
            //EventTimestamps.Add(timestamp);
            POIProxySessionManager.archiveSessionEvent(SessionId, poiEvent, timestamp);
        }

        public void archiveImageEvent(string userId, string mediaId, double timestamp)
        {
            archiveMediaEvent(userId, "image", mediaId, timestamp);
        }

        public void archiveVoiceEvent(string userId, string mediaId, double timestamp)
        {
            archiveMediaEvent(userId, "voice", mediaId, timestamp);
        }

        public void archiveIllustrationEvent(string userId, string mediaId, double timestamp)
        {
            archiveMediaEvent(userId, "illustration", mediaId, timestamp);
        }

        private void archiveMediaEvent(string userId, string type, string mediaId, double timestamp)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                EventIndex = EventList.Count,
                EventType = type,
                MediaId = mediaId,
                UserId = userId,
                Timestamp = timestamp,
                Message = ""
            };

            //EventList.Add(poiEvent);
            //EventTimestamps.Add(timestamp);

            POIProxySessionManager.archiveSessionEvent(SessionId, poiEvent, timestamp);
        }

        private void archiveSessionEvent(string userId, string type, double timestamp)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                EventIndex = EventList.Count,
                EventType = type,
                MediaId = "",
                UserId = userId,
                Timestamp = timestamp,
                Message = ""
            };

            //EventList.Add(poiEvent);
            //EventTimestamps.Add(timestamp);

            POIProxySessionManager.archiveSessionEvent(SessionId, poiEvent, timestamp);
        }

        public void archiveSessionCreatedEvent(string userId, double timestamp)
        {
            archiveSessionEvent(userId, "session_created", timestamp);
        }

        public void archiveSessionJoinedEvent(string userId, double timestamp)
        {
            archiveSessionEvent(userId, "session_joined", timestamp);
        }
    }

    public class POIInteractiveEvent
    {
        public int EventIndex { get; set; }
        public string EventType { get; set; }
        public string MediaId { get; set; }
        public string Message { get; set; }
        public string UserId { get; set; }
        public double Timestamp { get; set; }
    }

  
}