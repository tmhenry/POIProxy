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
    public class POIProxyUserManager
    {
        private static PooledRedisClientManager redisManager = POIProxyRedisManager.Instance.getRedisClientManager();
        private static POIProxyDbManager dbManager = POIProxyDbManager.Instance;
        private static JavaScriptSerializer jsonHandler = new JavaScriptSerializer();
        private static POIProxyInteractiveMsgHandler interMsgHandler = POIGlobalVar.Kernel.myInterMsgHandler;

        public static void onUpdateUserScore(string userId, string scoreCol, int value)
        {
            using (var redisClient = redisManager.GetClient())
            {

                Dictionary<string, object> userConditions = new Dictionary<string, object>();
                userConditions["user_id"] = userId;

                List<string> userCols = new List<string>();
                userCols.Add(scoreCol);

                DataRow userResult = interMsgHandler.getByUserId(userConditions, userCols, "user_score");
                int scoreValue = (int)userResult[scoreCol];

                interMsgHandler.updateByUserId(userId, scoreCol, scoreValue + value, "user_score");

                //get user school and team info from MySQL
                userConditions = new Dictionary<string, object>();
                userConditions["user_id"] = userId;
                userCols = new List<string>();
                userCols.Add("school");
                userCols.Add("team");
                DataRow userInfo = dbManager.selectSingleRowFromTable("user_profile", userCols, userConditions);

                if (userInfo == null)
                {
                    return;
                }

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
        }
    }
}