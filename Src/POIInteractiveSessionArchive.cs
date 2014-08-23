using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace POIProxy
{
    public class POIInteractiveSessionArchive
    {
        public string SessionId { get; set; }
        public Dictionary<string, string> Info { get; set; }
        public List<POIInteractiveEvent> EventList { 
            get { 
                return POIProxySessionManager.getSessionEventList(SessionId); 
            } 
        }

        public POIInteractiveSessionArchive(Dictionary<string, string> info)
        {
            SessionId = info["session_id"];
            Info = info;

            //Add the users to the session user list and archive the correponding events
            if (info["student_id"] != null)
            {
                archiveSessionCreatedEvent(info["student_id"], double.Parse(info["create_at"]));
            }

            if (info["tutor_id"] != null)
            {
                archiveSessionJoinedEvent(info["tutor_id"], double.Parse(info["start_at"]));
            }

            //Refresh the token count
            POIProxySessionManager.refreshSessionTokenPool(SessionId);
        }

        public bool checkUserInSession(string userId)
        {
            return POIProxySessionManager.checkUserInSession(SessionId, userId);
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
                //EventIndex = EventList.Count,
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
                //EventIndex = EventList.Count,
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

        private void archiveSessionEvent(string userId, string type, Dictionary<string, string> eventData, double timestamp)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = type,
                MediaId = "",
                UserId = userId,
                Timestamp = timestamp,
                Message = "",
                Data = eventData
            };

            POIProxySessionManager.archiveSessionEvent(SessionId, poiEvent, timestamp);
        }

        public void archiveSessionCreatedEvent(string userId, double timestamp)
        {
            var userInfo = POIProxySessionManager.getUserInfo(userId);

            if (!Info.ContainsKey("student_id"))
            {
                Info["student_id"] = userInfo["user_id"];
                Info["student_avatar"] = userInfo["avatar"];
                Info["student_name"] = userInfo["username"];
                Info["create_at"] = timestamp.ToString();
            }
            
            archiveSessionEvent(userId, "session_created", Info, timestamp);
        }

        public void archiveSessionJoinedEvent(string userId, double timestamp)
        {
            var userInfo = POIProxySessionManager.getUserInfo(userId);
            
            if (!Info.ContainsKey("tutor_id"))
            {
                //Update session info dictionary
                Info["tutor_id"] = userInfo["user_id"];
                Info["tutor_avatar"] = userInfo["avatar"];
                Info["tutor_name"] = userInfo["username"];
                Info["start_at"] = timestamp.ToString();
            }

            archiveSessionEvent(userId, "session_joined", userInfo, timestamp);
        }
    }
}