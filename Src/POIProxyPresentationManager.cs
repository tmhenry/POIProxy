using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;

using ServiceStack.Redis;
using ServiceStack.Redis.Generic;
using ServiceStack.Text;

using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace POIProxy
{
    public class POIProxyPresentationManager
    {
        private static PooledRedisClientManager redisManager = POIProxyRedisManager.Instance.getRedisClientManager();
        private static POIProxyDbManager dbManager = POIProxyDbManager.Instance;
        private static JavaScriptSerializer jsonHandler = new JavaScriptSerializer();

        private static POIProxyPresentationManager instance = null;
        public static POIProxyPresentationManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new POIProxyPresentationManager();
                }
                return instance;
            }
        }

        private POIProxyPresentationManager()
        {
        }

        public string onPresentationCreate(string msgId, string userId, string mediaId,
            string desc, string accessType = "private", string category = "")
        {
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            //Create interactive presentation
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["user_id"] = userId;
            values["type"] = "interactive";
            values["course_id"] = -1;
            values["description"] = desc;
            values["create_at"] = timestamp;
            values["media_id"] = mediaId;

            string presId = dbManager.insertIntoTable("presentation", values);

            //Get the information about the activity
            var userInfo = POIProxySessionManager.Instance.getUserInfo(userId);
            Dictionary<string, string> infoDict = new Dictionary<string, string>();
            infoDict["pres_id"] = presId;
            infoDict["create_at"] = timestamp.ToString();
            infoDict["creator"] = userId;
            infoDict["description"] = desc;
            infoDict["cover"] = mediaId;
            infoDict["access_type"] = accessType;
            infoDict["user_id"] = userInfo["user_id"];
            infoDict["username"] = userInfo["username"];
            infoDict["avatar"] = userInfo["avatar"];

            try
            {
                POIProxyPresentationManager.Instance.onPresentationUpdate(presId, infoDict);   
            }
            catch (Exception e)
            {
                PPLog.errorLog("redis error:" + e.Message);
            }

            //Archive the session created event
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "session_created",
                EventId = msgId.ToString(),
                MediaId = "",
                UserId = userId,
                Timestamp = timestamp,
                Message = "",
                Data = infoDict
            };
            //Subscribe the user to the session

            POIProxyPresentationManager.Instance.createPresentationEvent(presId, poiEvent);

            //Insert the question activity into the activity table
            addQuestionActivity(userId, presId);

            PPLog.debugLog("[POIProxyPresentationManager onPresentationCreate] presentation created! presentation id: " + presId);
            return presId;
        }

        public string onPresentationJoin(string msgId, string userId, string presId, string message)
        {
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            Dictionary<string, string> presInfo = getPresentationInfo(presId);

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["type"] = "interactive";
            values["presId"] = (presId == "-1") ? "0" : presId;
            values["creator"] = presInfo["creator"];
            values["tutor"] = userId;
            values["create_at"] = timestamp;
            values["start_at"] = timestamp;
            values["status"] = "serving";

            string sessionId = dbManager.insertIntoTable("session", values);

            //var userInfo = POIProxySessionManager.Instance.getUserInfo(userId);
            Dictionary<string, string> infoDict = new Dictionary<string, string>();
            infoDict["session_id"] = sessionId;
            infoDict["pres_id"] = presId;
            infoDict["create_at"] =  timestamp.ToString();
            infoDict["creator"] = presInfo["creator"];
            infoDict["description"] = presInfo["description"];
            infoDict["cover"] = presInfo["cover"];
            infoDict["access_type"] = presInfo["access_type"];
            infoDict["user_id"] = presInfo["user_id"];
            infoDict["username"] = presInfo["username"];
            infoDict["avatar"] = presInfo["avatar"];

            try {
                POIProxySessionManager.Instance.updateSessionInfo(sessionId, infoDict);
            }
            catch (Exception e)
            {
                PPLog.errorLog("redis error:" + e.Message);
            }            

            //Archive the session created event
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "session_created",
                EventId = msgId.ToString(),
                MediaId = "",
                UserId = presInfo["creator"],
                Timestamp = timestamp,
                Message = "",
                Data = infoDict
            };
            //Subscribe the user to the session
            POIProxySessionManager.Instance.subscribeSession(sessionId, userId);
            POIProxySessionManager.Instance.subscribeSession(sessionId, presInfo["creator"]);

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEvent);
            POIProxySessionManager.Instance.createSessionEvent(sessionId, poiEvent);

            createPresentationJoin(presId, sessionId, userId);

            //Insert the question activity into the activity table
            //addQuestionActivity(userId, sessionId);

            PPLog.debugLog("[POIProxyPresentationManager onPresentationJoin] session created! session id: " + sessionId);

            return sessionId;
        }

        public void onPresentationUpdate(string presId, Dictionary<string, string> update, string userId = "")
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessionInfo = redisClient.Hashes["presentation:presentation_info:" + presId];

                foreach (string key in update.Keys)
                {
                    //PPLog.infoLog("[DEBUG] userId: " + userId + " Key: " + key + " KeyValue: " + update[key]);
                    sessionInfo[key] = update[key];
                }
            }
        }

        public Dictionary<string, string> getPresentationInfo(string presId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var presInfo = redisClient.Hashes["presentation:presentation_info:" + presId];

                if (presInfo.Count == 0)
                {
                    //Read session info from db
                    //Get the pres id from session table
                    Dictionary<string, object> conditions = new Dictionary<string, object> 
                    { 
                        {"pid", presId}
                    };
                    var presRecord = dbManager.selectSingleRowFromTable("presentation", null, conditions);
                    
                    presInfo["pres_id"] = presId;
                    presInfo["create_at"] = presRecord["create_at"].ToString();
                    presInfo["creator"] = presRecord["user_id"] as string;
                    presInfo["cover"] = presRecord["media_id"] as string;
                    presInfo["description"] = presRecord["description"] as string;
                    presInfo["access_type"] = "private";

                    var userInfo = POIProxySessionManager.Instance.getUserInfo(presInfo["creator"]);
                    presInfo["user_id"] = userInfo["user_id"];
                    presInfo["username"] = userInfo["username"];
                    presInfo["avatar"] = userInfo["avatar"];
                }
                return redisClient.GetAllEntriesFromHash("presentation:presentation_info:" + presId);
            }
        }

        public void createPresentationEvent(string presId, POIInteractiveEvent poiEvent)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.Hashes["presentation:presentation_create_event"];
                eventList[poiEvent.EventId] = presId;
            }
        }

        public bool checkDuplicatedCreatedPresentation(string eventId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.Hashes["presentation:presentation_create_event"];
                if (eventList.ContainsKey(eventId))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public string getPresentationByMsgId(string msgId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.Hashes["presentation:presentation_create_event"];
                return eventList.ContainsKey(msgId) ? eventList[msgId] : "";
            }
        }

        public void createPresentationJoin(string presId, string sessionId, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessionList = redisClient.Hashes["presentation:presentation_session_list:" + presId];
                sessionList[userId] = sessionId;
            }
        }

        public bool checkDuplicatedPresentationJoin(string presId, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessionList = redisClient.Hashes["presentation:presentation_session_list:"+presId];
                if (sessionList.ContainsKey(userId))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public string getPresentationSessionIdByUserId(string presId, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessionList = redisClient.Hashes["presentation:presentation_session_list:" + presId];
                return sessionList.ContainsKey(userId) ? sessionList[userId] : "";
            }
        }

        public void addQuestionActivity(string userId, string presId)
        {
            var studentInfo = POIProxySessionManager.Instance.getUserInfo(userId);
            var presInfo = POIProxyPresentationManager.Instance.getPresentationInfo(presId);

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["user_id"] = userId;
            values["type"] = "int_pres_question";
            values["content_id"] = presId;
            values["create_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            values["data"] = jsonHandler.Serialize(
                new Dictionary<string, object>
                {
                    {"session", presInfo},
                    {"student", studentInfo},
                }
            );

            dbManager.insertIntoTable("activity", values);
        }
    }
}