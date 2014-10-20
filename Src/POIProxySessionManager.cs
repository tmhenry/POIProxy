using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using ServiceStack.Redis;
using ServiceStack.Redis.Generic;
using ServiceStack.Text;

using System.Data;

namespace POIProxy
{
    public class POIProxySessionManager
    {
        private static PooledRedisClientManager redisManager = new PooledRedisClientManager(POIGlobalVar.RedisHost + ":" + POIGlobalVar.RedisPort);
        private static POIProxyDbManager dbManager = POIProxyDbManager.Instance;

        public static void refreshSessionTokenPool(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var users = redisClient.Sets["user_by_session:" + sessionId];
                redisClient["session_user_count:" + sessionId] = users.Count.ToString();
            }
        }


        public static bool acquireSessionToken(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                //int maxNumUsers = checkPrivateTutoring(sessionId) ? 1 : 10;
                int maxNumUsers = 1;

                //PPLog.infoLog("In acquire session token, total is " + maxNumUsers);
                
                if (redisClient.Increment("session_user_count:" + sessionId, 1) <= maxNumUsers)
                {
                    return true;
                }
                else
                {
                    redisClient.Decrement("session_user_count:" + sessionId, 1);
                    return false;
                }
            }
        }

        public static void releaseSessionToken(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                redisClient.Decrement("session_user_count:" + sessionId, 1);
            }
        }

        public static void subscribeSession(string sessionId, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                var users = redisClient.Sets["user_by_session:" + sessionId];

                sessions[sessionId] = (0).ToString();
                users.Add(userId);
            }
        }

        public static void unsubscribeSession(string sessionId, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                var users = redisClient.Sets["user_by_session:" + sessionId];

                sessions.Remove(sessionId);
                users.Remove(userId);
            }

            releaseSessionToken(sessionId);
        }

        public static IRedisHash getSessionsByUserId(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return redisClient.Hashes["session_by_user:" + userId];
            }
        }

        public static List<string> getUsersBySessionId(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return redisClient.Sets["user_by_session:" + sessionId].ToList();
            }
        }

        public static List<Dictionary<string, string>> getUserListDetailsBySessionId(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var userList = redisClient.Sets["user_by_session:" + sessionId];

                var detailList = new List<Dictionary<string, string>>();
                foreach (string userId in userList)
                {
                    detailList.Add(getUserInfo(userId));
                }

                return detailList;
            }
        }

        public static bool checkUserInSession(string sessionId, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return redisClient.Sets["user_by_session:" + sessionId].Contains(userId);
            }
        }

        public static void updateSyncReference(string sessionId, string userId, double timestamp)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                sessions[sessionId] = timestamp.ToString();
            }
        }

        public static void archiveSessionEvent(string sessionId, POIInteractiveEvent poiEvent)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.As<POIInteractiveEvent>().GetHash<string>("archive:event_list:" + sessionId);
                eventList[poiEvent.EventId] = poiEvent;
            }
        }

        public static void createSessionEvent(string sessionId, POIInteractiveEvent poiEvent)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.Hashes["create_sessoin_event"];
                eventList[poiEvent.EventId] = sessionId;
            }
        }

        public static List<POIInteractiveEvent> getSessionEventList(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.As<POIInteractiveEvent>().GetHash<string>("archive:event_list:" + sessionId);
                var eventByTimestampList = new SortedDictionary<Double, POIInteractiveEvent>();
                
                foreach (string eventId in eventList.Keys)
                {
                    //PPLog.debugLog("[Session ID]"+sessionId+"  [eventId]"+eventId+"  [Timestamp]"+eventTimestampList[eventId]);
                    eventByTimestampList[eventList[eventId].Timestamp] = eventList[eventId];
                }
                return eventByTimestampList.Values.ToList();
            }
        }

        public static bool checkEventExists(string sessionId, string eventId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.As<POIInteractiveEvent>().GetHash<string>("archive:event_list:" + sessionId);
                if (eventList != null)
                {
                    return eventList.ContainsKey(eventId);
                }
                else
                {
                    return false;
                }
            }
        }

        public static bool checkDuplicatedCreatedSession(string eventId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.Hashes["create_sessoin_event"];
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

        public static string getSessionByMsgId(string msgId)
        {
            using (var redisClient = redisManager.GetClient()) {
                var sessionEvent = redisClient.Hashes["create_sessoin_event"];
                return sessionEvent[msgId];
            }
        }

        public static Dictionary<string,string> getUserInfo(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var userInfo = redisClient.Hashes["user:" + userId];

                if (userInfo.Count == 0 || !userInfo.ContainsKey("user_id") || !userInfo.ContainsKey("username") || !userInfo.ContainsKey("avatar") || !userInfo.ContainsKey("accessRight"))
                {
                    updateUserInfoFromDb(userId);
                }

                return redisClient.GetAllEntriesFromHash("user:" + userId);
            }
        }

        public static void updateUserInfoFromDb(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var userInfo = redisClient.Hashes["user:" + userId];
                Dictionary<string, object> conditions = new Dictionary<string, object>();
                List<string> cols = new List<string>();

                conditions["id"] = userId;
                cols.Add("username");
                cols.Add("avatar");
                cols.Add("accessRight");
                DataRow user = dbManager.selectSingleRowFromTable("users", cols, conditions);
                if (user != null)
                {
                    PPLog.infoLog("Read user info from mysql");
                    //Read user info from db and save into redis
                    userInfo["user_id"] = userId;
                    userInfo["username"] = user["username"].ToString();
                    userInfo["avatar"] = user["avatar"].ToString();
                    userInfo["accessRight"] = user["accessRight"].ToString();

                    //Find the user profile 
                    conditions.Clear();
                    cols.Clear();
                    conditions["user_id"] = userId;
                    cols.Add("school");
                    cols.Add("department");
                    cols.Add("rating");

                    DataRow profile = dbManager.selectSingleRowFromTable("user_profile", cols, conditions);
                    if (profile != null)
                    {
                        userInfo["rating"] = profile["rating"].ToString();

                        conditions.Clear();
                        cols.Clear();

                        conditions["sid"] = profile["school"];
                        cols.Add("name");
                        DataRow school = dbManager.selectSingleRowFromTable("school", cols, conditions);

                        if (school != null)
                        {
                            userInfo["school"] = school["name"] as string;
                        }
                        else
                        {
                            userInfo["school"] = "";
                        }

                        conditions.Clear();
                        cols.Clear();

                        conditions["did"] = profile["department"];
                        cols.Add("name");
                        DataRow dept = dbManager.selectSingleRowFromTable("department", cols, conditions);

                        if (dept != null)
                        {
                            userInfo["department"] = dept["name"] as string;
                        }
                        else
                        {
                            userInfo["department"] = "";
                        }
                    }
                }
            }

        }

        public static void updateUserDevice(string userId, string deviceId, string system, int tag)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var users = redisClient.Hashes["user_device:" + userId];
                var device = redisClient.Sets["device_by_system:" + system];
                if (users.ContainsKey("deviceId") && users["deviceId"] != "" && system == "ios") 
                {
                    device.Remove(users["deviceId"]);
                }
                users["deviceId"] = deviceId;
                users["system"] = system;
                users["tag"] = tag.ToString();

                if (system == "ios") {
                    if (tag == (int)POIGlobalVar.tag.SUBSCRIBED)
                    {
                        device.Add(deviceId);
                    }
                    else if (tag == (int)POIGlobalVar.tag.UNSUBSCRIBED)
                    {
                        device.Remove(deviceId);
                    }
                }
            }
        }

        public static IRedisHash getUserDevice(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            { 
                var users = redisClient.Hashes["user_device:" + userId];
                return users;
            }
        }

        public static List<string> getDeviceBySystem(string system)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return redisClient.Sets["device_by_system:" + system].ToList();
            }
        }

        public static Dictionary<string,string> getSessionInfo(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessionInfo = redisClient.Hashes["session:" + sessionId];

                if (sessionInfo.Count == 0)
                {
                    //Read session info from db
                    //Get the pres id from session table
                    Dictionary<string, object> conditions = new Dictionary<string, object> 
                    { 
                        {"id", sessionId}
                    };

                    var sessionRecord = dbManager.selectSingleRowFromTable("session", null, conditions);
                    
                    conditions.Clear();
                    conditions["pid"] = sessionRecord["presId"];
                    var presRecord = dbManager.selectSingleRowFromTable("presentation", null, conditions);

                    sessionInfo["session_id"] = sessionId;
                    sessionInfo["create_at"] = sessionRecord["create_at"].ToString();
                    sessionInfo["creator"] = sessionRecord["creator"] as string;
                    sessionInfo["cover"] = presRecord["media_id"] as string;
                    sessionInfo["description"] = presRecord["description"] as string;
                }
                return redisClient.GetAllEntriesFromHash("session:" + sessionId);
            }
        }

        public static List<Dictionary<string, string>> getSessionDetail(List<string> sessionList, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var detailList = new List<Dictionary<string, string>>();
                foreach (string sessionId in sessionList)
                {
                    var sessionInfo = getSessionInfo(sessionId);
                    Dictionary<string, string> sessionTempDic = new Dictionary<string, string>();
                    sessionTempDic["sessionId"] = sessionInfo["session_id"];
                    sessionTempDic["vote"] = sessionInfo.ContainsKey("vote") ? sessionInfo["vote"] : "0";
                    sessionTempDic["watch"] = sessionInfo.ContainsKey("watch") ? sessionInfo["watch"] : "0";
                    var session_vote_by_user = redisClient.Hashes["session_vote_by_user:" + userId];
                    if (session_vote_by_user.ContainsKey(sessionId) && session_vote_by_user[sessionId] == (0).ToString())
                        sessionTempDic["isVoted"] = "1";
                    else
                        sessionTempDic["isVoted"] = "0";
                    detailList.Add(sessionTempDic);
                }
                return detailList;
            }
        }

        public static void updateSessionInfo(string sessionId, Dictionary<string, string> update, string userId = "")
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessionInfo = redisClient.Hashes["session:" + sessionId];

                foreach (string key in update.Keys)
                {
                    //PPLog.infoLog("[DEBUG] userId: " + userId + " Key: " + key + " KeyValue: " + update[key]);
                    if (key == "vote" || key == "watch")
                    {
                        if (sessionInfo[key] == null) {
                            sessionInfo[key] = (0).ToString();
                        }
                        sessionInfo[key] = (int.Parse(sessionInfo[key]) + (int.Parse(update[key]) > 1 ? 1 : int.Parse(update[key]))).ToString();
                    }
                    else 
                    { 
                        sessionInfo[key] = update[key];
                    }
                }

                if (userId != null && userId != "")
                {
                    
                    var session_vote_by_user = redisClient.Hashes["session_vote_by_user:" + userId];
                    foreach (string key in update.Keys)
                    {
                        if (key == "vote" && update[key] != "0")
                        {
                            session_vote_by_user[sessionId] = (0).ToString();
                        }
                    }
                    
                    var session_watch_by_user = redisClient.Hashes["session_watch_by_user:" + userId];
                    foreach (string key in update.Keys)
                    {
                        if (key == "watch" && update[key] != "0")
                        {
                            session_watch_by_user[sessionId] = (0).ToString();
                        }
                    }
                }
            }
        }

        public static POISessionArchive getSessionArchive(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return new POISessionArchive
                {
                    SessionId = sessionId,
                    UserList = getUserListDetailsBySessionId(sessionId),
                    EventList = getSessionEventList(sessionId),
                    Info = getSessionInfo(sessionId)
                };
            }
        }

        public static bool checkPrivateTutoring(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessionInfo = redisClient.Hashes["session:" + sessionId];

                PPLog.infoLog("[POIProxySessionManager checkPrivateTutoring] Access type is : " + sessionInfo["access_type"]);

                if (sessionInfo["access_type"] == "group")
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        

    }
}