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
    public class POIPresentation
    {
        public Dictionary<string, string> InfoDict
        {
            get
            {
                Dictionary<string, string> result = new Dictionary<string,string>();
                result["presId"] = presId;
                result["creator"] = creatorId;
                result["type"] = presType;
                result["description"] = description;
                result["media_id"] = mediaId;
                result["create_at"] = createTimestamp.ToString();
                result["update_at"] = updateTimestamp.ToString();
                result["cid"] = categoryId.ToString();
                result["gid"] = gradeId.ToString();
                result["sid"] = subjectId.ToString();
                result["difficulty"] = difficulty.ToString();
                result["vanilla"] = vanillaFlag;
                result["interactive"] = interactiveSessionId;
                return result;
            }
        }

        public void LoadPresInfo(string presId)
        {
            
        }

        public static bool LoadPresInfoFromOrigin(string presId)
        {
            using (var redisClient = redisManager.GetClient())
            {
                var presInfo = redisClient.Hashes[POIProxyRedisManager.CachePresentation + presId];

                //Load presentation info from Database
                Dictionary<string, object> conditions = new Dictionary<string, object>();
                conditions["pid"] = presId;
                var dbResult = dbManager.selectSingleRowFromTable("presentation", null, conditions);

                
                if (dbResult != null)
                {

                }

                return true;
            }
        }

        // Handlers
        private static PooledRedisClientManager redisManager = POIProxyRedisManager.Instance.getRedisClientManager();
        private static POIProxyDbManager dbManager = POIProxyDbManager.Instance;
        private static JavaScriptSerializer jsonHandler = new JavaScriptSerializer();

        // Premitives
        protected string presId;
        protected string creatorId;
        protected string presType;
        protected string description;
        protected string mediaId;
        protected double createTimestamp;
        protected double updateTimestamp;
        protected int categoryId;
        protected int gradeId;
        protected int subjectId;
        protected int difficulty;
        protected string vanillaFlag;
        protected string interactiveSessionId;
    }
}