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

        public POIInteractiveSessionArchive(string sessionId, Dictionary<string, string> info)
        {
            SessionId = sessionId;
            Info = info;

            UserList = new List<string>();
            EventList = new List<POIInteractiveEvent>();
        }

        public void addUserToUserList(string userId)
        {
            UserList.Add(userId);
        }

        public void archiveTextEvent(string userId, string message)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                EventIndex = EventList.Count,
                EventType = "text",
                MediaId = "",
                UserId = userId,
                TimeStamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now),
                Message = message
            };

            EventList.Add(poiEvent);
        }

        public void archiveImageEvent(string userId, string mediaId)
        {
            archiveMediaEvent(userId, "image", mediaId);
        }

        public void archiveVoiceEvent(string userId, string mediaId)
        {
            archiveMediaEvent(userId, "voice", mediaId);
        }

        public void archiveIllustrationEvent(string userId, string mediaId)
        {
            archiveMediaEvent(userId, "illustration", mediaId);
        }

        private void archiveMediaEvent(string userId, string type, string mediaId)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                EventIndex = EventList.Count,
                EventType = type,
                MediaId = mediaId,
                UserId = userId,
                TimeStamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now),
                Message = ""
            };

            EventList.Add(poiEvent);
        }

        private void archiveSessionEvent(string userId, string type)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                EventIndex = EventList.Count,
                EventType = type,
                MediaId = "",
                UserId = userId,
                TimeStamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now),
                Message = ""
            };

            EventList.Add(poiEvent);
        }

        public void archiveSessionCreatedEvent(string userId)
        {
            archiveSessionEvent(userId, "session_created");
        }

        public void archiveSessionJoinedEvent(string userId)
        {
            archiveSessionEvent(userId, "session_joined");
        }
    }

    public class POIInteractiveEvent
    {
        public int EventIndex { get; set; }
        public string EventType { get; set; }
        public string MediaId { get; set; }
        public string Message { get; set; }
        public string UserId { get; set; }
        public double TimeStamp { get; set; }
    }
}