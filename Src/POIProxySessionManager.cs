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
        //private static PooledRedisClientManager redisManager = new PooledRedisClientManager(POIGlobalVar.RedisHost + ":" + POIGlobalVar.RedisPort) { ConnectTimeout = 500 };
        private static PooledRedisClientManager redisManager = POIProxyRedisManager.Instance.getRedisClientManager();
        private static POIProxyDbManager dbManager = POIProxyDbManager.Instance;
        private static JavaScriptSerializer jsonHandler = new JavaScriptSerializer();

        private static POIProxySessionManager instance;
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

        private POIProxySessionManager()
        {
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

        public void deleteSession(string sessionId, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                sessions[sessionId] = (-1).ToString();
                updateSyncReference(sessionId, userId, -1);
            }
        }

        public bool checkIsDeletedSession(string sessionId, string userId)
        { 
            using (var redisClient = redisManager.GetClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                if (sessions[sessionId] == "-1")
                {
                    return true;
                }
                else 
                {
                    return false;
                }
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

        public IRedisList getServiceByUserId(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return redisClient.Lists["archive:service_list:" + userId];
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
                if (sessionId != POIGlobalVar.customerSession.ToString())
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
                else {
                    //var serviceList = redisClient.As<POIInteractiveEvent>().GetHash<string>("archive:service_list:" + poiEvent.UserId);
                    //serviceList[poiEvent.Timestamp.ToString()] = poiEvent;
                    if (poiEvent.UserId != "")
                    {
                        var customerList = redisClient.As<POIInteractiveEvent>().Lists["archive:service_list:" + poiEvent.UserId];
                        customerList.Push(poiEvent);
                        var serviceList = redisClient.As<POIInteractiveEvent>().Lists["archive:service_list:" + poiEvent.CustomerId];
                        serviceList.Push(poiEvent);
                        var serviceLatestUser = redisClient.As<string>().SortedSets["archive:service_latest_user"];
                        redisClient.AddItemToSortedSet("archive:service_latest_user", poiEvent.UserId, poiEvent.Timestamp);
                    }
                }
            }
        }

        public void updateSyncReference(string sessionId, string userId, double timestamp)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                if (sessions[sessionId] != "-1") {
                    sessions[sessionId] = timestamp.ToString();
                }
                
                List<object> sessionList = new List<object>();
                foreach (var session in sessions) {
                    if (session.Value != "0" && session.Value != "-1") {
                        Dictionary<string, object> sessionDic = new Dictionary<string, object>();
                        sessionDic[session.Key] = session.Value;
                        sessionList.Add(sessionDic);
                    }
                }

                string session_by_user = jsonHandler.Serialize(sessionList);
                
                var user = redisClient.Hashes["user:" + userId];
                user["status"] = GetMd5Hash(session_by_user);
            }
        }

        public bool checkSyncReference(string userId, string hash)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var user = redisClient.Hashes["user:" + userId];

                if (!user.ContainsKey("status"))
                {
                    user["status"] = GetMd5Hash("[]");
                }
                if (String.Equals(hash, (string)user["status"], StringComparison.OrdinalIgnoreCase))
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

        public List<POIInteractiveEvent> getSessionEventList(string sessionId, bool sortByTimestamp = true)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.As<POIInteractiveEvent>().GetHash<string>("archive:event_list:" + sessionId);

                if (sortByTimestamp)
                {
                    var eventByTimestampList = new SortedDictionary<Double, POIInteractiveEvent>();

                    foreach (string eventId in eventList.Keys)
                    {
                        eventByTimestampList[eventList[eventId].Timestamp] = eventList[eventId];
                    }
                    return eventByTimestampList.Values.ToList();
                }
                else
                {
                    return eventList.Values.ToList();
                }
            }
        }

        public List<POIInteractiveEvent> getAnswerEventList(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.As<POIInteractiveEvent>().GetHash<double>("session:public_answer:" + sessionId);
                var eventByTimestampList = new SortedDictionary<Double, POIInteractiveEvent>();

                foreach (double timestamp in eventList.Keys)
                {
                    //PPLog.debugLog("[Session ID]"+sessionId+"  [eventId]"+eventId+"  [Timestamp]"+eventTimestampList[eventId]);
                    eventByTimestampList[timestamp] = eventList[timestamp];
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
                    var userInfo = redisClient.Hashes["user:" + userId];
                    
                    var device = redisClient.Sets["device_by_system:" + system];
                    if (users.ContainsKey("deviceId") && users["deviceId"] != "")
                    {
                        device.Remove(users["deviceId"]);
                    }
                    if (tag == (int)POIGlobalVar.tag.SUBSCRIBED)
                    {
                        if (userInfo.ContainsKey("accessRight") && userInfo["accessRight"] == "tutor")
                        {
                            device.Add(deviceId);
                        }  
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
                    sessionInfo["media_id"] = sessionRecord["media_id"] as string;
                }
                return redisClient.GetAllEntriesFromHash("session:" + sessionId);
            }
        }

        public List<Dictionary<string, string>> getSessionDetail(List<string> sessionList, string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var detailList = new List<Dictionary<string, string>>();
                var userScoreList = redisClient.GetAllWithScoresFromSortedSet("user_ranking");
                foreach (string sessionId in sessionList)
                {
                    var sessionInfo = getSessionInfo(sessionId);
                    Dictionary<string, string> sessionTempDic = new Dictionary<string, string>();
                    sessionTempDic["userScore"] = sessionInfo.ContainsKey("tutor") ?
                        (userScoreList.ContainsKey(sessionInfo["tutor"]) ? (int)userScoreList[sessionInfo["tutor"]] : 0).ToString() : "0";
                    sessionTempDic["sessionId"] = sessionId;
                    sessionTempDic["vote"] = sessionInfo.ContainsKey("vote") ? sessionInfo["vote"] : "0";
                    sessionTempDic["watch"] = sessionInfo.ContainsKey("watch") ? sessionInfo["watch"] : "0";
                    sessionTempDic["adopt"] = sessionInfo.ContainsKey("adopt") ? sessionInfo["adopt"] : "0";
                    
                    sessionTempDic["submitted"] = sessionInfo.ContainsKey("submitted") ? sessionInfo["submitted"] : "0";
                    if (sessionInfo.ContainsKey("submitted") && sessionInfo["submitted"] == "1")
                    {
                        sessionTempDic["preview"] = getSessionAnswerPreview(sessionId);
                    }

                    sessionTempDic["score"] = getSessionScore(sessionInfo).ToString();
                    var session_vote_by_user = redisClient.Hashes["session_vote_by_user:" + userId];
                    if (session_vote_by_user.ContainsKey(sessionId) && session_vote_by_user[sessionId] == (0).ToString())
                    {
                        sessionTempDic["isVoted"] = "1";
                    }
                    else if (session_vote_by_user.ContainsKey(sessionId) && session_vote_by_user[sessionId] == (-1).ToString())
                    {
                        sessionTempDic["isVoted"] = "-1";
                    }
                    else
                    {
                        sessionTempDic["isVoted"] = "0";
                    }
                    
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
                    else if (key == "adopt")
                    {
                        if (update[key] == "1" || update[key] == "2")
                        {
                            sessionInfo[key] = update[key];
                        }
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
                            if (update[key] == "1")
                            {
                                session_vote_by_user[sessionId] = (0).ToString();
                            }
                            else if (update[key] == "-1")
                            {
                                session_vote_by_user[sessionId] = (-1).ToString();
                            }
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
            double fScore = nVote + Math.Log(nWatch + 1) + (fCreateTime - fCurrentTime) / 86400 / 2;

            return fScore;
        }

        public static string GetMd5Hash (string input)
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

        public string DictToString<T, V>(IEnumerable<KeyValuePair<T, V>> items, string format)
        {
            format = String.IsNullOrEmpty(format) ? "{0}='{1}' " : format;

            System.Text.StringBuilder itemString = new System.Text.StringBuilder();
            foreach (var item in items)
                itemString.AppendFormat(format, item.Key, item.Value);

            return itemString.ToString();
        }

        public static bool checkUserBanList(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var banList = redisClient.Hashes["ban_list"];
                var userDevice = redisClient.Hashes["user_device:"+userId];
                if (userDevice.ContainsKey("deviceId"))
                {
                    string deviceId = userDevice["deviceId"];
                    if (banList.ContainsKey(deviceId))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        public void submitSessionAnswer(string sessionId, List<string> answerList)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessionInfo = redisClient.Hashes["session:" + sessionId];
                var sessionEventList = redisClient.Hashes["archive:event_list:" + sessionId];
                var answerHash = redisClient.Hashes["session:public_answer:" + sessionId];
                answerHash.Clear();
                
                if (answerList.IsNullOrEmpty())
                {
                    sessionInfo["submitted"] = "0";
                    return;
                }
                
                sessionInfo["submitted"] = "1";

                foreach (var answerItem in answerList)
                {
                    if (sessionEventList.ContainsKey("\"" + answerItem + "\""))
                    {
                        string msg = sessionEventList["\"" + answerItem + "\""];
                        Dictionary<string, object> msgInfo = jsonHandler.Deserialize<Dictionary<string, object>>(msg);
                        string timestamp = msgInfo["Timestamp"].ToString();
                        answerHash[timestamp] = msg;
                    }
                }
            }
        }

        public List<object>retrieveSessionAnswer(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var answerList = new List<object>();

                var eventList = getAnswerEventList(sessionId);

                if (eventList != null)
                {
                    for (int i = 0; i < eventList.Count; i++)
                    {
                        Dictionary<string, object> message = new Dictionary<string, object>();

                        int eventType = (int)POIGlobalVar.resource.SESSIONS;

                        message["msgId"] = eventList[i].EventId;
                        message["userId"] = eventList[i].UserId;
                        message["sessionId"] = sessionId;
                        message["timestamp"] = eventList[i].Timestamp;

                        if (eventList[i].EventType == "session_created")
                        {
                            message["sessionType"] = POIGlobalVar.sessionType.CREATE;
                            message["mediaId"] = eventList[i].MediaId;
                            var sessionInfo = POIProxySessionManager.Instance.getSessionInfo(sessionId);
                            message["description"] = sessionInfo.ContainsKey("description") ? sessionInfo["description"] : "";
                            message["presId"] = sessionInfo.ContainsKey("pres_id") ? sessionInfo["pres_id"] : "0";
                        }
                        else if (eventList[i].EventType == "session_joined")
                        {
                            message["sessionType"] = POIGlobalVar.sessionType.JOIN;
                            message["message"] = eventList[i].Message;
                        }
                        else if (eventList[i].EventType == "session_cancelled")
                        {
                            message["sessionType"] = POIGlobalVar.sessionType.CANCEL;
                        }
                        else if (eventList[i].EventType == "session_ended")
                        {
                            message["sessionType"] = POIGlobalVar.sessionType.END;
                        }
                        else if (eventList[i].EventType == "session_rated")
                        {
                            message["sessionType"] = POIGlobalVar.sessionType.RATING;
                            var sessionInfo = POIProxySessionManager.Instance.getSessionInfo(sessionId);
                            message["rating"] = sessionInfo.ContainsKey("rating") ? sessionInfo["rating"] : "0";
                        }

                        else if (eventList[i].EventType == "text")
                        {
                            message["msgType"] = POIGlobalVar.messageType.TEXT;
                            eventType = (int)POIGlobalVar.resource.MESSAGES;
                        }
                        else if (eventList[i].EventType == "voice")
                        {
                            message["msgType"] = POIGlobalVar.messageType.VOICE;
                            eventType = (int)POIGlobalVar.resource.MESSAGES;
                        }
                        else if (eventList[i].EventType == "image")
                        {
                            message["msgType"] = POIGlobalVar.messageType.IMAGE;
                            eventType = (int)POIGlobalVar.resource.MESSAGES;
                        }
                        else if (eventList[i].EventType == "illustration")
                        {
                            message["msgType"] = POIGlobalVar.messageType.ILLUSTRATION;
                            eventType = (int)POIGlobalVar.resource.MESSAGES;
                        }
                        else
                        {
                            message["msgType"] = POIGlobalVar.sessionType.GET;
                        }

                        if (eventType == (int)POIGlobalVar.resource.SESSIONS)
                        {
                            message["resource"] = POIGlobalVar.resource.SESSIONS;
                            message["userInfo"] = jsonHandler.Serialize(POIProxySessionManager.Instance.getUserInfo(eventList[i].UserId));
                        }
                        else
                        {
                            message["resource"] = POIGlobalVar.resource.MESSAGES;
                            message["message"] = eventList[i].Message;
                            message["mediaId"] = eventList[i].MediaId;
                            message["mediaDuration"] = eventList[i].MediaDuration;
                        }

                        answerList.Add(message);
                    }
                }
                return answerList;
            }
        }

        public string getIllustationMetaInfo(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessionInfo = redisClient.Hashes["session:" + sessionId];
                if (!sessionInfo.ContainsKey("media_id"))
                {
                    Dictionary<string, object> conditions = new Dictionary<string, object> 
                    { 
                        {"id", sessionId}
                    };

                    var sessionRecord = dbManager.selectSingleRowFromTable("session", null, conditions);

                    sessionInfo["media_id"] = sessionRecord["media_id"] as string;
                    
                }

                string result = sessionInfo["media_id"];
                return result;
            }
        }

        public string getSessionAnswerPreview(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                PPLog.debugLog("[GET SESSION ANSWER PREVIEW]: IN FUNC. sessionId: " + sessionId);
                
                string result = "";

                var sessionInfo = redisClient.Hashes["session:" + sessionId];

                if (sessionInfo.ContainsKey("submitted"))
                {
                    if (sessionInfo["submitted"] == "1")
                    {
                        var sessionAnswerList = getAnswerEventList(sessionId);

                        /*
                        var eventList = new SortedDictionary<double, string>();

                        foreach (string answerItem in sessionAnswerDict.Keys)
                        {
                            eventList[double.Parse(answerItem)] = sessionAnswerDict[answerItem];
                        }
                        */

                        const int TEXT_MAX_COUNT = 1;
                        const int IMAGE_MAX_COUNT = 3;
                        const int ILLUSTRATION_MAX_COUNT = 1;
                        
                        List<Dictionary<string, string>> textList = new List<Dictionary<string, string>>();
                        List<Dictionary<string, string>> imageList = new List<Dictionary<string, string>>();
                        List<Dictionary<string, string>> illustrationList = new List<Dictionary<string, string>>();

                        int textCount = 0;
                        int imageCount = 0;
                        int illustrationCount = 0;

                        foreach (POIProxy.POIInteractiveEvent eventItem in sessionAnswerList)
                        {
                            string eventType = eventItem.EventType;
                            //PPLog.debugLog("[GET SESSION ANSWER PREVIEW]:sessionId: " + sessionId + " eventType: " + eventItem.EventType);

                            if (eventType == "text" && textCount < TEXT_MAX_COUNT)
                            {
                                Dictionary<string, string> tempDict = new Dictionary<string, string>();
                                tempDict["timestamp"] = eventItem.Timestamp.ToString();
                                tempDict["message"] = eventItem.Message;
                                textList.Add(tempDict);
                                textCount++;
                                //PPLog.debugLog("[GET SESSION ANSWER PREVIEW]: sessionId: " + sessionId + " Text Count: " + textCount.ToString());
                            }
                            else if (eventType == "image" && imageCount < IMAGE_MAX_COUNT)
                            {
                                Dictionary<string, string> tempDict = new Dictionary<string, string>();
                                tempDict["timestamp"] = eventItem.Timestamp.ToString();
                                tempDict["media_id"] = eventItem.MediaId;
                                imageList.Add(tempDict);
                                imageCount++;
                                //PPLog.debugLog("[GET SESSION ANSWER PREVIEW]: sessionId: " + sessionId + " Image Count: " + imageCount.ToString());
                            }
                            else if (eventType == "illustration" && illustrationCount < ILLUSTRATION_MAX_COUNT)
                            {
                                Dictionary<string, string> tempDict = new Dictionary<string, string>();
                                tempDict["timestamp"] = eventItem.Timestamp.ToString();
                                tempDict["session_id"] = eventItem.MediaId;
                                tempDict["media_id"] = getIllustationMetaInfo(tempDict["session_id"]);
                                illustrationList.Add(tempDict);
                                illustrationCount++;
                                //PPLog.debugLog("[GET SESSION ANSWER PREVIEW]: sessionId: " + sessionId + " Illustration Count: " + illustrationCount.ToString());
                            }
                        }

                        Dictionary<string, string> returnDict = new Dictionary<string, string>();
                        returnDict["text"] = jsonHandler.Serialize(textList);
                        returnDict["image"] = jsonHandler.Serialize(imageList);
                        returnDict["illustration"] = jsonHandler.Serialize(illustrationList);

                        PPLog.debugLog("[GET SESSION ANSWER PREVIEW]: sessionId: " + sessionId + " preview: " + jsonHandler.Serialize(returnDict));
                        result = jsonHandler.Serialize(returnDict);
                    }
                }


                return result;
            }
        }
    }
}