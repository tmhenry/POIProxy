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
            string desc, string accessType = "private", string filter= "")
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

            Dictionary<string, string> filterInfo = jsonHandler.Deserialize<Dictionary<string, string>>(filter);
            values.Clear();
            values["pid"] = presId;
            if (filter != "")
            {
                values["gid"] = filterInfo.ContainsKey("gid") ? filterInfo["gid"] : "0";
                values["sid"] = filterInfo.ContainsKey("sid") ? filterInfo["sid"] : "0";
                values["cid"] = filterInfo.ContainsKey("cid") ? filterInfo["cid"] : "0";
            }
            else
            {
                values["gid"] = "0";
                values["sid"] = "0";
                values["cid"] = "0";
            }
            dbManager.insertIntoTable("pres_category", values);

            //Get the information about the activity
            var userInfo = POIProxySessionManager.Instance.getUserInfo(userId);
            Dictionary<string, string> infoDict = new Dictionary<string, string>();
            infoDict["pres_id"] = presId;
            infoDict["create_at"] = timestamp.ToString();
            infoDict["creator"] = userId;
            infoDict["description"] = desc;
            infoDict["cover"] = mediaId;
            infoDict["cid"] = filterInfo.ContainsKey("cid") ? filterInfo["cid"] : "0";
            infoDict["gid"] = filterInfo.ContainsKey("gid") ? filterInfo["gid"] : "0";
            infoDict["sid"] = filterInfo.ContainsKey("sid") ? filterInfo["sid"] : "0";
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
                EventType = "presentation_created",
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

            updateUserPresentation(userId, presId, (int)POIGlobalVar.presentationType.CREATE);

            PPLog.debugLog("[POIProxyPresentationManager onPresentationCreate] presentation created! presentation id: " + presId);
            return presId;
        }

        public string onPresentationJoin(string msgId, string userId, string presId, string message, double timestamp)
        {
            //double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

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

            POIInteractiveEvent poiEventJoin = new POIInteractiveEvent
            {
                EventType = "session_joined",
                EventId = Guid.NewGuid().ToString(),
                MediaId = "",
                UserId = userId,
                Timestamp = timestamp,
                Message = message,
                Data = POIProxySessionManager.Instance.getUserInfo(userId)
            };
            
            //Subscribe the user to the session
            POIProxySessionManager.Instance.subscribeSession(sessionId, userId);
            POIProxySessionManager.Instance.subscribeSession(sessionId, presInfo["creator"]);

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEvent);
            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEventJoin);

            POIProxySessionManager.Instance.createSessionEvent(sessionId, poiEvent);

            createPresentationJoin(presId, sessionId, userId);

            //Insert the question activity into the activity table
            //addQuestionActivity(userId, sessionId);

            PPLog.debugLog("[POIProxyPresentationManager onPresentationJoin] session created! session id: " + sessionId);

            updateUserPresentation(userId, presId, (int)POIGlobalVar.presentationType.JOIN);

            return sessionId;
        }

        public void onPresentationUpdate(string presId, Dictionary<string, string> update, string userId = "")
        {
            using (var redisClient = redisManager.GetClient())
            {
                var presInfo = redisClient.Hashes["presentation:presentation_info:" + presId];

                foreach (string key in update.Keys)
                {
                    //PPLog.infoLog("[DEBUG] userId: " + userId + " Key: " + key + " KeyValue: " + update[key]);
                    presInfo[key] = update[key];
                }
            }
        }

        public List<Dictionary<string, string>> onPresentationGet(List<string> presList, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
                foreach (string presId in presList)
                {
                    Dictionary<string, string> userSessionDict = redisClient.GetAllEntriesFromHash("presentation:presentation_session_list:" + presId);
                    
                    Dictionary<string, string> presTempDict = new Dictionary<string, string>();
                    presTempDict["presId"] = presId;

                    int sessionCount = 0;
                    int submitCount = 0;
                    bool adoptFlag = false;

                    foreach (string tutorId in userSessionDict.Keys)
                    {
                        string sessionId = userSessionDict[tutorId];

                        var sessionInfo = redisClient.Hashes["session:" + sessionId];
                        if (sessionInfo.ContainsKey("adopt") && sessionInfo["adopt"] == "1")
                        {
                            adoptFlag = true;
                        }
                        if (sessionInfo.ContainsKey("submitted") && sessionInfo["submitted"] == "1")
                        {
                            submitCount++;
                        }
                        sessionCount++;
                    }

                    presTempDict["adopt"] = (adoptFlag) ? "1" : "0";
                    presTempDict["submitCount"] = submitCount.ToString();
                    presTempDict["sessionCount"] = sessionCount.ToString();

                    result.Add(presTempDict);
                }
                return result;
            }
        }

        public List<Dictionary<string, string>> onPresentationQuery(List<string> presList)
        {
            List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();

            foreach (string presId in presList)
            {
                result.Add(getPresentationInfo(presId));
            }
            
            return result;
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

                Dictionary<string, string> resultDict = new Dictionary<string, string>();
                resultDict["pres_id"] = presInfo.ContainsKey("pres_id") ? presInfo["pres_id"] : "";
                resultDict["create_at"] = presInfo.ContainsKey("create_at") ? presInfo["create_at"] : "";
                resultDict["creator"] = presInfo.ContainsKey("creator") ? presInfo["creator"] : "";
                resultDict["description"] = presInfo.ContainsKey("description") ? presInfo["description"] : "";
                resultDict["cover"] = presInfo.ContainsKey("cover") ? presInfo["cover"] : "";
                resultDict["cid"] = presInfo.ContainsKey("cid") ? presInfo["cid"] : "0";
                resultDict["gid"] = presInfo.ContainsKey("gid") ? presInfo["gid"] : "0";
                resultDict["sid"] = presInfo.ContainsKey("sid") ? presInfo["sid"] : "0";
                resultDict["access_type"] = presInfo.ContainsKey("access_type") ? presInfo["access_type"] : "";
                resultDict["user_id"] = presInfo.ContainsKey("user_id") ? presInfo["user_id"] : "";
                resultDict["username"] = presInfo.ContainsKey("username") ? presInfo["username"] : "";
                resultDict["avatar"] = presInfo.ContainsKey("avatar") ? presInfo["avatar"] : "";

                return resultDict;
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

        public void updateUserPresentation(string userId, string presId, int type)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var userPres = redisClient.Hashes["presentation:presentation_by_user:" + userId];
                userPres[presId] = type.ToString();
                updatePresSync(userId);
            }
        }

        public List<string> getUserPresentationList(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var userPres = redisClient.Hashes["presentation:presentation_by_user:" + userId];

                SortedDictionary<int, string> presSoretedDict = new SortedDictionary<int, string>();
                foreach (string key in userPres.Keys)
                {
                    if (userPres[key] != "-1")
                    {
                        presSoretedDict[int.Parse(key)] = key;
                    }
                }

                List<string> presList = new List<string>();
                foreach (int key in presSoretedDict.Keys)
                {
                    presList.Add(presSoretedDict[key]);
                }

                return presList;
            }
        }

        public void updatePresSync(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var userPres = redisClient.Hashes["presentation:presentation_by_user:" + userId];
                
                SortedDictionary<int, string> presSoretedDict = new SortedDictionary<int, string>();
                foreach (string key in userPres.Keys)
                {
                    if (userPres[key] != "-1")
                    {
                        presSoretedDict[int.Parse(key)] = key;
                    }
                }

                List<int> presList = new List<int>();
                foreach (int key in presSoretedDict.Keys)
                {
                    presList.Add(key);
                }

                string presByUser = jsonHandler.Serialize(presList);

                var user = redisClient.Hashes["user:" + userId];
                user["statusPres"] = POIProxySessionManager.GetMd5Hash(presByUser);
            }
        }

        public bool checkPresSync(string userId, string hash)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var user = redisClient.Hashes["user:" + userId];

                if (!user.ContainsKey("statusPres"))
                {
                    user["statusPres"] = POIProxySessionManager.GetMd5Hash("[]");
                }
                if (String.Equals(hash, (string)user["statusPres"], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}