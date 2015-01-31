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
        POIProxyInteractiveMsgHandler interMsgHandler = POIGlobalVar.Kernel.myInterMsgHandler;

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
            values["type"] = "idle";
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
            infoDict["realname"] = userInfo["realname"];
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

        public string onPresentationJoin(string msgId, string userId, string presId, double timestamp, string messageList = "")
        {
            //double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            Dictionary<string, string> presInfo = getPresentationInfo(presId);

            if (userId == presInfo["creator"])
            {
                return "";
            }

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["type"] = "interactive";
            values["presId"] = (presId == "-1") ? "0" : presId;
            values["creator"] = presInfo["creator"];
            values["tutor"] = userId;
            values["create_at"] = timestamp;
            values["start_at"] = timestamp;
            values["status"] = "serving";
            string sessionId = dbManager.insertIntoTable("session", values);

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["pid"] = presId;
            values = new Dictionary<string, object>();
            values["type"] = "interactive";
            dbManager.updateTable("presentation", values, conditions);

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
            infoDict["tutor"] = userId;

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
                Message = "",
                Data = POIProxySessionManager.Instance.getUserInfo(userId)
            };
            
            //Subscribe the user to the session
            POIProxySessionManager.Instance.subscribeSession(sessionId, userId);

            if (checkCreatorPresStatus(presId))
                POIProxySessionManager.Instance.subscribeSession(sessionId, presInfo["creator"]);

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEvent);
            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEventJoin);

            POIProxySessionManager.Instance.createSessionEvent(sessionId, poiEvent);

            createPresentationJoin(presId, sessionId, userId);

            //Insert the question activity into the activity table
            //addQuestionActivity(userId, sessionId);

            PPLog.debugLog("[POIProxyPresentationManager onPresentationJoin] session created! session id: " + sessionId);

            updateUserPresentation(userId, presId, (int)POIGlobalVar.presentationType.JOIN);

            string pushMsg = jsonHandler.Serialize(new
            {
                resource = POIGlobalVar.resource.SESSIONS,
                sessionType = POIGlobalVar.sessionType.INVITE,
                msgId = msgId,
                userId = userId,
                userInfo = jsonHandler.Serialize(POIProxySessionManager.Instance.getUserInfo(userId)),
                sessionId = sessionId,
                presId = presId,
                timestamp = timestamp,
                message = "",
            });

            List<string> userList = POIProxySessionManager.Instance.getUsersBySessionId(sessionId);
            userList.Remove(userId);
            POIProxyPushNotifier.send(userList, pushMsg);

            archiveSessionAnswer(messageList, sessionId, userId);

            return sessionId;
        }

        public double onPresentationPrepare(string presId, string userId, double timestamp, int prepareTime)
        {
            using (var redisClient = redisManager.GetClient())
            {
                if (prepareTime > 0)
                {
                    double targetTime = timestamp + prepareTime * 60;
                    redisClient.AddItemToSortedSet("presentation:presentation_prepare_list:" + presId, userId, targetTime);
                    return targetTime;
                }
                else if (prepareTime == -1)
                {
                    if (redisClient.SortedSetContainsItem("presentation:presentation_prepare_list:" + presId, userId))
                    {
                        redisClient.RemoveItemFromSortedSet("presentation:presentation_prepare_list:" + presId, userId);
                    }
                    return (double)0;
                }
                return (double)0;
            }
        }

        public void onPresentationUpdate(string presId, Dictionary<string, string> update, string userId = "")
        {
            using (var redisClient = redisManager.GetClient())
            {
                var presInfo = redisClient.Hashes["presentation:presentation_info:" + presId];

                foreach (string key in update.Keys)
                {
                    //PPLog.infoLog("[DEBUG] userId: " + userId + " Key: " + key + " KeyValue: " + update[key]);
                    if (key.Equals("difficulty"))
                    {
                        if (update[key].Equals("1") && userId != "")
                        {
                            var userPresVote = redisClient.Hashes["presentation:presentation_user_difficulty:" + userId];
                            if (!userPresVote.ContainsKey(presId))
                            {
                                int difficulty = presInfo.ContainsKey("difficulty") ? int.Parse(presInfo["difficulty"]) : 0;
                                difficulty++;
                                presInfo["difficulty"] = difficulty.ToString();
                                userPresVote[presId] = "0";
                            }
                        }
                    }
                    else
                    {
                        presInfo[key] = update[key];
                    }
                }
            }
        }

        public List<Dictionary<string, string>> onPresentationGet(List<string> presList, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
                var userPresVote = redisClient.Hashes["presentation:presentation_user_difficulty:" + userId];

                foreach (string presId in presList)
                {
                    Dictionary<string, string> userSessionDict = redisClient.GetAllEntriesFromHash("presentation:presentation_session_list:" + presId);
                    var presInfo = redisClient.Hashes["presentation:presentation_info:" + presId];

                    Dictionary<string, string> presTempDict = new Dictionary<string, string>();
                    presTempDict["presId"] = presId;
                    presTempDict["vanilla"] = (presInfo.ContainsKey("vanilla") && presInfo["vanilla"] == "1") ? presInfo["vanilla"] : "0";
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

                    presTempDict["difficulty"] = presInfo.ContainsKey("difficulty") ? presInfo["difficulty"] : "0";
                    presTempDict["votedDifficulty"] = (userId != "" && userPresVote.ContainsKey(presId)) ? "1" : "0";

                    int prepareCount = 0;
                    var prepareDict = redisClient.GetAllWithScoresFromSortedSet("presentation:presentation_prepare_list:" + presId);
                    List<Dictionary<string, string>> prepareList = new List<Dictionary<string, string>>();
                    foreach (string user in prepareDict.Keys)
                    {
                        var userInfo = POIProxySessionManager.Instance.getUserInfo(user);

                        if (prepareDict[user] >= POITimestamp.ConvertToUnixTimestamp(DateTime.Now))
                        {
                            Dictionary<string, string> prepareTempDict = new Dictionary<string, string>();
                            prepareTempDict["userId"] = user;
                            prepareTempDict["targetTime"] = prepareDict[user].ToString();
                            prepareTempDict["avatar"] = userInfo["avatar"];

                            prepareList.Add(prepareTempDict);
                            prepareCount++;
                        }
                    }

                    presTempDict["prepareCount"] = prepareCount.ToString();
                    presTempDict["prepareList"] = jsonHandler.Serialize(prepareList);

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

        public void onPresentationDelete(string presId, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                if (userId == null || userId == "")
                {
                    return;
                }

                var userPres = redisClient.Hashes["presentation:presentation_by_user:" + userId];
                userPres[presId] = (-1).ToString();
                updatePresSync(userId);
                
                Dictionary<string, string> presInfo = getPresentationInfo(presId);
                var sessionList = redisClient.Hashes["presentation:presentation_session_list:" + presId];
                var sessions = redisClient.Hashes["session_by_user:" + userId];

                if (userId == presInfo["creator"])
                {
                    foreach (string tutorId in sessionList.Keys)
                    {
                        string sessionId = sessionList[tutorId];
                        sessions[sessionId] = (-1).ToString();
                        POIProxySessionManager.Instance.updateSyncReference(sessionId, userId, -1);

                        var users = redisClient.Sets["user_by_session:" + sessionId];
                        users.Remove(userId);
                    }
                }
                else
                {
                    if (sessionList.ContainsKey(userId))
                    {
                        string sessionId = sessionList[userId];
                        sessions[sessionId] = (-1).ToString();
                        POIProxySessionManager.Instance.updateSyncReference(sessionId, userId, -1);

                        var users = redisClient.Sets["user_by_session:" + sessionId];
                        users.Remove(userId);
                    }
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
                    presInfo["realname"] = userInfo["realname"];
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
                resultDict["realname"] = presInfo.ContainsKey("realname") ? presInfo["realname"] : "";
                resultDict["avatar"] = presInfo.ContainsKey("avatar") ? presInfo["avatar"] : "";
                resultDict["vanilla"] = presInfo.ContainsKey("vanilla") ? presInfo["vanilla"] : "0";
                    
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

                if (redisClient.SortedSetContainsItem("presentation:presentation_prepare_list:" + presId, userId))
                {
                    redisClient.RemoveItemFromSortedSet("presentation:presentation_prepare_list:" + presId, userId);
                }
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

        private void archiveSessionAnswer(string messageList, string sessionId, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                if (messageList == null || messageList == "")
                {
                    return;
                }
                var msgList = jsonHandler.Deserialize<List<Dictionary<string, string>>>(messageList);

                List<string> userList = POIProxySessionManager.Instance.getUsersBySessionId(sessionId);
                userList.Remove(userId);

                var sessionInfo = redisClient.Hashes["session:" + sessionId];
                sessionInfo["submitted"] = "1";

                var answerHash = redisClient.Hashes["session:public_answer:" + sessionId];
                var sessionEventList = redisClient.Hashes["archive:event_list:" + sessionId];

                foreach (Dictionary<string, string> msgInfo in msgList)
                {
                    string msgStr = msgInfo["msgType"];
                    int msgType = 0;
                    //int msgType = int.Parse(msgInfo["msgType"]);

                    if (msgStr == "text")
                    {
                        msgType = (int)POIGlobalVar.messageType.TEXT;
                    }
                    else if (msgStr == "voice")
                    {
                        msgType = (int)POIGlobalVar.messageType.VOICE;
                    }
                    else if (msgStr == "image")
                    {
                        msgType = (int)POIGlobalVar.messageType.IMAGE;
                    }

                    string message = msgInfo.ContainsKey("message") ? msgInfo["message"] : "";
                    string mediaId = msgInfo.ContainsKey("mediaId") ? msgInfo["mediaId"] : "";
                    float mediaDuration = msgInfo.ContainsKey("mediaDuration") ? float.Parse(msgInfo["mediaDuration"]) : 0;

                    string msgId = Guid.NewGuid().ToString();
                    double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                    string customerId = POIGlobalVar.customerId;

                    string pushMsg = jsonHandler.Serialize(new
                    {
                        resource = POIGlobalVar.resource.MESSAGES,
                        msgId = msgId,
                        userId = userId,
                        sessionId = sessionId,
                        msgType = msgType,
                        message = message,
                        mediaId = mediaId,
                        mediaDuration = mediaDuration,
                        timestamp = timestamp
                    });

                    POIInteractiveEvent poiEvent = new POIInteractiveEvent
                    {
                        EventType = "",
                        EventId = msgId,
                        UserId = userId,
                        Timestamp = timestamp,
                        Message = message,
                        MediaId = mediaId,
                        CustomerId = customerId,
                    };

                    switch (msgType)
                    {
                        case (int)POIGlobalVar.messageType.TEXT:
                            poiEvent.EventType = "text";
                            //POIProxyToWxApi.textMsgReceived(userList, sessionId, message);
                            break;
                        case (int)POIGlobalVar.messageType.IMAGE:
                            poiEvent.EventType = "image";
                            //POIProxyToWxApi.imageMsgReceived(userList, sessionId, mediaId);
                            break;
                        case (int)POIGlobalVar.messageType.VOICE:
                            poiEvent.EventType = "voice";
                            //POIProxyToWxApi.voiceMsgReceived(userList, sessionId, mediaId);
                            break;
                        case (int)POIGlobalVar.messageType.ILLUSTRATION:
                            poiEvent.EventType = "illustration";
                            //POIProxyToWxApi.illustrationMsgReceived(userList, sessionId, mediaId);
                            break;
                        case (int)POIGlobalVar.messageType.SYSTEM:
                            poiEvent.EventType = "system";
                            //POIProxyToWxApi.textMsgReceived(userList, sessionId, message);
                            break;
                        default:
                            break;
                    }
                    POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEvent);
                    POIProxyPushNotifier.send(userList, pushMsg);
                    string msg = sessionEventList["\"" + msgId + "\""];
                    answerHash[timestamp.ToString()] = msg;
                }
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

        public bool checkCreatorPresStatus(string presId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var presInfo = getPresentationInfo(presId);
                var userId = presInfo["creator"];
                var userPres = redisClient.Hashes["presentation:presentation_by_user:" + userId];

                return (userPres.ContainsKey(presId) && userPres[presId] != "-1");
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