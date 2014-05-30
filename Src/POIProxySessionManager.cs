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
        private static PooledRedisClientManager redisManager = new PooledRedisClientManager("localhost:6379");
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
                int maxNumUsers = checkPrivateTutoring(sessionId) ? 1 : 10;

                POIGlobalVar.POIDebugLog("In acquire session token, total is " + maxNumUsers);
                
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

        public static void archiveSessionEvent(string sessionId, POIInteractiveEvent poiEvent, double timestamp)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.As<POIInteractiveEvent>().GetHash<double>("archive:event_list:" + sessionId);
                eventList[timestamp] = poiEvent;
            }
        }

        public static List<POIInteractiveEvent> getSessionEventList(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.As<POIInteractiveEvent>().GetHash<double>("archive:event_list:" + sessionId);
                return eventList.Values.ToList();
            }
        }

        public static bool checkEventExists(string sessionId, double timestamp)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var eventList = redisClient.As<POIInteractiveEvent>().GetHash<double>("archive:event_list:" + sessionId);
                if (eventList != null)
                {
                    return eventList.ContainsKey(timestamp);
                }
                else
                {
                    return false;
                }
            }
        }

        public static Dictionary<string,string> getUserInfo(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var userInfo = redisClient.Hashes["user:" + userId];

                if (userInfo.Count == 0)
                {
                    POIGlobalVar.POIDebugLog("Read user info from db");

                    //Read user info from db and save into redis
                    userInfo["user_id"] = userId;

                    Dictionary<string, object> conditions = new Dictionary<string, object>();
                    List<string> cols = new List<string>();

                    conditions["id"] = userId;
                    cols.Add("username");
                    cols.Add("avatar");

                    DataRow user = dbManager.selectSingleRowFromTable("users", cols, conditions);
                    if (user != null)
                    {
                        userInfo["username"] = user["username"] as string;
                        userInfo["avatar"] = user["avatar"] as string;

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

                return redisClient.GetAllEntriesFromHash("user:" + userId);
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

        public static void updateSessionInfo(string sessionId, Dictionary<string, string> update)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var sessionInfo = redisClient.Hashes["session:" + sessionId];

                foreach (string key in update.Keys)
                {
                    sessionInfo[key] = update[key];
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

                POIGlobalVar.POIDebugLog("Access type is : " + sessionInfo["access_type"]);

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