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
        #region Functions communicating with redis server

        public static void subscribeSession(string sessionId, string userId)
        {
            using (var redisClient = new RedisClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                var users = redisClient.Sets["user_by_session:" + sessionId];

                sessions[sessionId] = (0).ToString();
                users.Add(userId);
            }
        }

        public static void unsubscribeSession(string sessionId, string userId)
        {
            using (var redisClient = new RedisClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                var users = redisClient.Sets["user_by_session:" + sessionId];

                sessions.Remove(sessionId);
                users.Remove(userId);
            }
        }

        public static IRedisHash getSessionsByUserId(string userId)
        {
            using (var redisClient = new RedisClient())
            {
                return redisClient.Hashes["session_by_user:" + userId];
            }
        }

        public static IRedisSet getUsersBySessionId(string sessionId)
        {
            using (var redisClient = new RedisClient())
            {
                return redisClient.Sets["user_by_session:" + sessionId];
            }
        }

        public static void updateSyncReference(string sessionId, string userId, double timestamp)
        {
            using (var redisClient = new RedisClient())
            {
                var sessions = redisClient.Hashes["session_by_user:" + userId];
                sessions[sessionId] = timestamp.ToString();
            }
        }

        public static POIInteractiveSessionArchive getArchiveBySessionId(string sessionId)
        {
            using (var redisClient = new RedisClient())
            {
                POIGlobalVar.POIDebugLog("get archive by session id");

                var redis = redisClient.As<POIInteractiveSessionArchive>();
                var hash = redis.GetHash<string>("archive");

                return hash[sessionId];
            }
        }

        public static POIInteractiveSessionArchive initSessionArchive(Dictionary<string, string> info)
        {
            using(var redisClient = new RedisClient())
            {
                POIGlobalVar.POIDebugLog("Init session archive");

                var redis = redisClient.As<POIInteractiveSessionArchive>();
                POIInteractiveSessionArchive archive = new POIInteractiveSessionArchive(info);
                POIGlobalVar.POIDebugLog(archive.SessionId);
                POIGlobalVar.POIDebugLog(archive.EventList.Count);

                var hash = redis.GetHash<string>("archive");
                hash[archive.SessionId] = archive;
                //redis.GetAndSetValue("archive:" + archive.SessionId, archive);

                return archive;
            }
        }

        #endregion
    }
}