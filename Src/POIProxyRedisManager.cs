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
    public class POIProxyRedisManager
    {
        private PooledRedisClientManager redisManager = null;

        private static POIProxyRedisManager instance = null;
        public static POIProxyRedisManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new POIProxyRedisManager();
                }
                return instance;
            }
        }

        private POIProxyRedisManager()
        {
            if (redisManager == null)
            {
                redisManager = new PooledRedisClientManager(POIGlobalVar.RedisHost + ":" + POIGlobalVar.RedisPort);
                redisManager.ConnectTimeout = 500;
                redisManager.IdleTimeOutSecs = 30;
                redisManager.PoolTimeout = 3;
            }
        }

        public PooledRedisClientManager getRedisClientManager()
        {
            return redisManager;
        }
    }
}