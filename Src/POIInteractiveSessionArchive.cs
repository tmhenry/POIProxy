using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using POILibCommunication;

namespace POIProxy
{
    public class POIInteractiveSessionArchive
    {
        public string SessionId;
        public Dictionary<string,string> Info;
        public List<string> UserList;
        public List<POIInteractiveEvent> EventList;

        //For checking duplicate messages
        private List<double> EventTimestamps;

        //For checking session state
        private readonly object statusLock = new object();
        private string Status;

        public POIInteractiveSessionArchive(Dictionary<string, string> info)
        {
            SessionId = info["session_id"];
            Info = info;

            UserList = new List<string>();
            EventList = new List<POIInteractiveEvent>();
            EventTimestamps = new List<double>();

            //Update the archive status
            lock (statusLock)
            {
                Status = info["status"];
            }

            //Add the users to the session user list and archive the correponding events
            if (info["student_id"] != null)
            {
                addUserToUserList(info["student_id"]);
                archiveSessionCreatedEvent(info["student_id"]);
            }

            if (info["tutor_id"] != null)
            {
                addUserToUserList(info["tutor_id"]);
                archiveSessionJoinedEvent(info["tutor_id"]);
            }
        }

        public bool joinSessionIfOpen()
        {
            lock (statusLock)
            {
                if (Status == "open")
                {
                    POIGlobalVar.POIDebugLog("Session is open!");
                    Status = "serving";
                    return true;
                }
                else
                {
                    return false;
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
            return EventTimestamps.Contains(eventTimestamp);
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

            EventList.Add(poiEvent);
            EventTimestamps.Add(timestamp);
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

            EventList.Add(poiEvent);
            EventTimestamps.Add(timestamp);
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

            EventList.Add(poiEvent);
            EventTimestamps.Add(timestamp);
        }

        public void archiveSessionCreatedEvent(string userId)
        {
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            archiveSessionEvent(userId, "session_created", timestamp);
        }

        public void archiveSessionJoinedEvent(string userId)
        {
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
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