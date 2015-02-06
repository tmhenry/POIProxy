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
    public class POIProxyPresentationActivityHandler
    {
        private static PooledRedisClientManager redisManager = POIProxyRedisManager.Instance.getRedisClientManager();
        private static POIProxyDbManager dbManager = POIProxyDbManager.Instance;
        private static JavaScriptSerializer jsonHandler = new JavaScriptSerializer();
        private static POIProxyInteractiveMsgHandler interMsgHandler = POIGlobalVar.Kernel.myInterMsgHandler;

        public enum PresentationAcitivity
        {
            CREATE = 0,
            PREPARE = 1,
            EXTEND = 2,
            CANCEL = 3,
            READY = 4,
        };

        public static void CreatePresentationActivity(PresentationAcitivity activity,
            string presId, string msgId, string userId, double timestamp, int value = 0)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var msgList = redisClient.Hashes["model:presentation_activity:" + msgId];
                msgList["msgId"] = msgId;
                msgList["presId"] = presId;
                msgList["userId"] = userId;
                msgList["timestamp"] = timestamp.ToString();

                switch (activity)
                {
                    case PresentationAcitivity.CREATE:
                        msgList["type"] = "CREATE";
                        break;
                    case PresentationAcitivity.PREPARE:
                        msgList["type"] = "PREPARE";
                        msgList["value"] = value > 0 ? value.ToString() : (0).ToString();
                        break;
                    case PresentationAcitivity.EXTEND:
                        msgList["type"] = "EXTEND";
                        msgList["value"] = value > 0 ? value.ToString() : (0).ToString();
                        break;
                    case PresentationAcitivity.CANCEL:
                        msgList["type"] = "CANCEL";
                        break;
                    case PresentationAcitivity.READY:
                        msgList["type"] = "READY";
                        break;
                    default:
                        break;
                }

                redisClient.AddItemToSortedSet("presentation:presentation_activity:" + presId, msgId, timestamp);
            }
        }

        public static List<Dictionary<string, string>> GetPresentationActivity(string presId, double lastTimestamp = 0.0)
        {
            using (var redisClient = redisManager.GetClient())
            {
                List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
                var presActivity = redisClient.GetAllWithScoresFromSortedSet("presentation:presentation_activity:" + presId);
                foreach (string key in presActivity.Keys)
                {
                    if (presActivity[key] > lastTimestamp)
                    {
                        //var activityInfo = redisClient.Hashes["model:presentation_activity:" + key];
                        Dictionary<string, string> tempDict = redisClient.GetAllEntriesFromHash("model:presentation_activity:" + key);
                        if (tempDict.ContainsKey("userId") && tempDict["userId"] == null && tempDict["userId"] == "")
                        {
                            var userInfo = POIProxySessionManager.Instance.getUserInfo(tempDict["userId"]);
                            tempDict["realname"] = userInfo["realname"];
                            tempDict["school"] = userInfo["school"];
                        }
                        result.Add(tempDict);
                    }
                }
                return result;
            }
        }

        //Private constructor
        private POIProxyPresentationActivityHandler()
        {
        }
    }
}