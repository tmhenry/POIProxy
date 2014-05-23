using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using ServiceStack.Redis;
using ServiceStack.Redis.Generic;
using ServiceStack.Text;

using POILibCommunication;

namespace POIProxy
{
    public class POIProxySessionManager
    {
        private static PooledRedisClientManager redisManager = new PooledRedisClientManager("localhost:6379");

        #region Functions communicating with redis server

        public static bool acquireSessionToken(string sessionId, int maxNumUsers)
        {
            using (var redisClient = redisManager.GetClient())
            {
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
        }

        public static IRedisHash getSessionsByUserId(string userId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return redisClient.Hashes["session_by_user:" + userId];
            }
        }

        public static IRedisSet getUsersBySessionId(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                return redisClient.Sets["user_by_session:" + sessionId];
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

        /*
        public static POIInteractiveSessionArchive getArchiveBySessionId(string sessionId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                POIGlobalVar.POIDebugLog("get archive by session id");

                var redis = redisClient.As<POIInteractiveSessionArchive>();
                return redis["archive:" + sessionId];
            }
        }

        public static POIInteractiveSessionArchive initSessionArchive(Dictionary<string, string> info)
        {
            using (var redisClient = redisManager.GetClient())
            {
                POIGlobalVar.POIDebugLog("Init session archive");

                var redis = redisClient.As<POIInteractiveSessionArchive>();
                POIInteractiveSessionArchive archive = new POIInteractiveSessionArchive(info);
                return redis.GetAndSetValue("archive:" + archive.SessionId, archive);
            }
        }*/

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

        #endregion
    }
}