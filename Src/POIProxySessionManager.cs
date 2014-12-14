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
    public class POIProxySessionManager
    {
        private static PooledRedisClientManager redisManager = new PooledRedisClientManager(POIGlobalVar.RedisHost + ":" + POIGlobalVar.RedisPort) { ConnectTimeout = 500 };
        private static POIProxyDbManager dbManager = POIProxyDbManager.Instance;
        private static JavaScriptSerializer jsonHandler = new JavaScriptSerializer();

        private static POIProxySessionManager instance;
        private POIProxySessionManager() { }
        public static POIProxySessionManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new POIProxySessionManager();
                }
                return instance;
            }
        }

        public void refreshSessionTokenPool(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var users = redisClient.Sets["user_by_session:" + sessionId];
                redisClient["session_user_count:" + sessionId] = users.Count.ToString();
            }
        }


        public bool acquireSessionToken(string sessionId)
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

        public void releaseSessionToken(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                redisClient.Decrement("session_user_count:" + sessionId, 1);
            }
        }

        public void subscribeSession(string sessionId, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                var users = redisClient.Sets["user_by_session:" + sessionId];

                sessions[sessionId] = (0).ToString();
                users.Add(userId);
            }
        }

        public void unsubscribeSession(string sessionId, string userId)
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

        public IRedisHash getSessionsByUserId(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return redisClient.Hashes["session_by_user:" + userId];
            }
        }

        public List<string> getUsersBySessionId(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return redisClient.Sets["user_by_session:" + sessionId].ToList();
            }
        }

        public List<Dictionary<string, string>> getUserListDetailsBySessionId(string sessionId)
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

        public bool checkUserInSession(string sessionId, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return redisClient.Sets["user_by_session:" + sessionId].Contains(userId);
            }
        }

        /*public void updateSyncReference(string sessionId, string userId, double timestamp)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                sessions[sessionId] = timestamp.ToString();
            }
        }*/

        public void archiveSessionEvent(string sessionId, POIInteractiveEvent poiEvent)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.As<POIInteractiveEvent>().GetHash<string>("archive:event_list:" + sessionId);
                eventList[poiEvent.EventId] = poiEvent;

                List<string> userList = getUsersBySessionId(sessionId);
                userList.Remove(poiEvent.UserId);
                foreach (string userId in userList)
                {
                    updateSyncReference(sessionId, userId, poiEvent.Timestamp);
                }
            }
        }

        public void updateSyncReference(string sessionId, string userId, double timestamp)
        {
            PPLog.debugLog("updateSyncReference: " + sessionId + " userId: " + userId + " timestamp: " + timestamp.ToString());
            using (var redisClient = redisManager.GetClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                sessions[sessionId] = timestamp.ToString();

                List<object> sessionList = new List<object>();
                foreach (var session in sessions) {
                    if (session.Value != "0") {
                        Dictionary<string, object> sessionDic = new Dictionary<string, object>();
                        sessionDic[session.Key] = session.Value;
                        sessionList.Add(sessionDic);
                    }
                }

                string session_by_user = jsonHandler.Serialize(sessionList);
                PPLog.debugLog("userId: " + userId + ": " + session_by_user);
                
                var user = redisClient.Hashes["user:" + userId];
                user["status"] = GetMd5Hash(session_by_user);
                PPLog.debugLog("[POIProxySessionManager] updateSyncReference Hash: " + GetMd5Hash(session_by_user) + "session dictionary: " + session_by_user);
            }
        }

        public bool checkSyncReference(string userId, string hash)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var user = redisClient.Hashes["user:" + userId];
                //PPLog.debugLog("[POIProxySessionManager] checkSyncReference Hash: " + user["status"]);
                if (hash == user["status"])
                {
                    return true;
                }
                else 
                {
                    return false;
                }
            }
        }

        public void createSessionEvent(string sessionId, POIInteractiveEvent poiEvent)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.Hashes["create_sessoin_event"];
                eventList[poiEvent.EventId] = sessionId;
            }
        }

        public List<POIInteractiveEvent> getSessionEventList(string sessionId)
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

        public bool checkEventExists(string sessionId, string eventId)
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

        public bool checkDuplicatedCreatedSession(string eventId)
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

        public string getSessionByMsgId(string msgId)
        {
            using (var redisClient = redisManager.GetClient()) {
                var sessionEvent = redisClient.Hashes["create_sessoin_event"];
                return sessionEvent[msgId];
            }
        }

        public Dictionary<string,string> getUserInfo(string userId)
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

        public void updateUserInfoFromDb(string userId)
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

        public void updateUserDevice(string userId, string deviceId, string system, int tag)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var users = redisClient.Hashes["user_device:" + userId];
                if (users.ContainsKey("deviceId") && users["deviceId"] != "" && users["deviceId"] != deviceId)
                {
                    double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                    string pushMsg = jsonHandler.Serialize(new
                    {
                        resource = POIGlobalVar.resource.USERS,
                        userType = POIGlobalVar.userType.LOGOUT,
                        userId = userId,
                        title="由于您的账号在其他设备上成功登录，即时消息会发送到其他设备上，如果想继续使用，请再次登录",
                        timestamp = timestamp,
                    });
                    List<string> userList = new List<string>();
                    userList.Add(userId);
                    POIProxyPushNotifier.send(userList, pushMsg);
                }
                users["deviceId"] = deviceId;
                users["system"] = system;
                users["tag"] = tag.ToString();

                if (system == "ios")
                {
                    var device = redisClient.Sets["device_by_system:" + system];
                    if (users.ContainsKey("deviceId") && users["deviceId"] != "")
                    {
                        device.Remove(users["deviceId"]);
                    }
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

        public IRedisHash getUserDevice(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            { 
                var users = redisClient.Hashes["user_device:" + userId];
                return users;
            }
        }

        public List<string> getDeviceBySystem(string system)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return redisClient.Sets["device_by_system:" + system].ToList();
            }
        }

        public Dictionary<string,string> getSessionInfo(string sessionId)
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

        public List<Dictionary<string, string>> getSessionDetail(List<string> sessionList, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var detailList = new List<Dictionary<string, string>>();
                foreach (string sessionId in sessionList)
                {
                    var sessionInfo = getSessionInfo(sessionId);
                    Dictionary<string, string> sessionTempDic = new Dictionary<string, string>();
                    sessionTempDic["sessionId"] = sessionId;
                    sessionTempDic["vote"] = sessionInfo.ContainsKey("vote") ? sessionInfo["vote"] : "0";
                    sessionTempDic["watch"] = sessionInfo.ContainsKey("watch") ? sessionInfo["watch"] : "0";
                    sessionTempDic["score"] = getSessionScore(sessionInfo).ToString();
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

        public void updateSessionInfo(string sessionId, Dictionary<string, string> update, string userId = "")
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

        public POISessionArchive getSessionArchive(string sessionId)
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

        public bool checkPrivateTutoring(string sessionId)
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

        public void updateUserScoreRanking(string userId, int value)
        {
            //get user school and team info from MySQL
            Dictionary<string, object> userConditions = new Dictionary<string, object>();
            userConditions["user_id"] = userId;
            List<string> userCols = new List<string>();
            userCols.Add("school");
            userCols.Add("team");
            DataRow userInfo = dbManager.selectSingleRowFromTable("user_profile", userCols, userConditions);

            if (userInfo == null)
            {
                return;
            }

            /*
            PPLog.debugLog("[DEBUG: updateuserScoreRanking] userId:" + userId + " value:" + value.ToString());
            if (userInfo["school"].ToString().Equals(null))
            {
                PPLog.debugLog("[DEBUG: updateuserScoreRanking] school:" + userInfo["school"].ToString());  
            }
            if (userInfo["team"].ToString().Equals(null))
            {
                PPLog.debugLog("[DEBUG: updateuserScoreRanking] team:" + userInfo["team"].ToString());
            }
            */

            using (var redisClient = redisManager.GetClient())
            {
                redisClient.IncrementItemInSortedSet("user_ranking", userId, value);
                redisClient.IncrementItemInSortedSet("user_ranking_weekly", userId, value);
                if (!userInfo["school"].ToString().Equals(null))
                {
                    redisClient.IncrementItemInSortedSet("user_ranking_by_school:" + userInfo["school"].ToString(), userId, value);
                    redisClient.IncrementItemInSortedSet("user_ranking_weekly_by_school:" + userInfo["school"].ToString(), userId, value);
                    redisClient.IncrementItemInSortedSet("ranking_by_school", userInfo["school"].ToString(), value);
                    redisClient.IncrementItemInSortedSet("ranking_weekly_by_school", userInfo["school"].ToString(), value);
                }
                if (!userInfo["team"].ToString().Equals(null))
                {
                    redisClient.IncrementItemInSortedSet("user_ranking_by_team:" + userInfo["team"].ToString(), userId, value);
                    redisClient.IncrementItemInSortedSet("user_ranking_weekly_by_team:" + userInfo["team"].ToString(), userId, value);
                    redisClient.IncrementItemInSortedSet("ranking_by_team", userInfo["team"].ToString(), value);
                    redisClient.IncrementItemInSortedSet("ranking_weekly_by_team", userInfo["team"].ToString(), value);
                }
            }
            return;
        }

        private double getSessionScore(Dictionary<string, string> sessionInfo)
        {
            int nVote = int.Parse(sessionInfo.ContainsKey("vote") ? sessionInfo["vote"] : "0");
            int nWatch = int.Parse(sessionInfo.ContainsKey("watch") ? sessionInfo["watch"] : "0");
            double fCreateTime = double.Parse(sessionInfo.ContainsKey("create_at") ? sessionInfo["create_at"] : "0");
            double fCurrentTime = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
            double fScore = nVote * 5 + Math.Log(nWatch + 1) + (fCreateTime - fCurrentTime) / 86400 / 2;

            //PPLog.debugLog("[Session Score] Session Id:" + sessionInfo["session_id"] + " Score:" + fScore.ToString() + " Vote:" + nVote.ToString() + " Watch:" + nWatch.ToString() + " CreateAt:" + fCreateTime.ToString() + " CurrentTime:" + fCurrentTime.ToString());
            return fScore;
        }

        private string GetMd5Hash (string input)
        {

            // Convert the input string to a byte array and compute the hash. 
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes 
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data  
            // and format each one as a hexadecimal string. 
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string. 
            return sBuilder.ToString();
        }
    }
}