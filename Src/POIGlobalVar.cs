using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace POIProxy
{
    //Define all the global variables here
    public static class POIGlobalVar
    {
        public static POIProxyKernel Kernel { get; set; }

        public static String ProxyServerIP { get; set; }
        public static int ProxyServerPort { get; set; }
        public static String ContentServerHome { get; set; }
        public static String DNSServerHome { get; set; }
        public static String Uploader { get; set; }

        public static String ProxyHost { get; set; }
        public static int ProxyPort { get; set; }
        public static String DbHost { get; set; }
        public static String DbName { get; set; }
        public static String DbUsername { get; set; }
        public static String DbPassword { get; set; }

        public static String RedisHost { get; set; }
        public static String RedisPort { get; set; }

        public static String KeywordsFileName { get { return "POI_Keywords.txt"; } }
        public static String KeywordsFileType { get { return ".txt"; } }

        public static int MaxMobileClientCount { get; set; }

        public enum resource { SESSIONS, MESSAGES, USERS, SERVICES, ALERTS, SYNC, PRESENTATIONS };
        public enum sessionType { CREATE, JOIN, END, CANCEL, UPDATE, RERAISE, RATING, GET, DELETE, INVITE, SUBMIT_ANSWER, RETRIEVE_ANSWER };
        public enum messageType { TEXT, IMAGE, VOICE, ILLUSTRATION, SYSTEM };
        public enum userType { UPDATE, SCORE, LOGOUT };
        public enum serviceType { SYSTEM, ACTION, NEWS, EXTRA, TASK };
        public enum alertType { SYSTEM };
        public enum syncType { SESSION, PRESENTATION };
        public enum presentationType { CREATE, JOIN, END, PREPARE, UPDATE, GET, QUERY, DELETE}
        public enum tag { UNSUBSCRIBED, SUBSCRIBED};

        public enum sessionAction { JOIN, VOTES, WATCH };
        public static int customerSession = 0;
        public static string customerId = "6c3514d4ad2741109e5b2a66dc2036df";

        public enum errorCode
        {
            SUCCESS = 0,
            DUPLICATED = 1,
            FAIL = 2,
            TIME_LIMITED = 1001,
            ALREADY_JOINED = 1002,
            TAKEN_BY_OTHERS = 1003,
            STUDENT_CANNOT_JOIN = 1004,
            TUTOR_CANNOT_RATING = 1005,
            STUDENT_CANNOT_END = 1006,
            SESSION_NOT_OPEN = 1007,
            SESSION_SYNC = 5001,
            SESSION_ASYNC = 5002,
        };

        public static Dictionary<int, string> errorMsg = new Dictionary<int, string>()
        {
            //GENERAL
            {0, "执行成功"},
            {1, "重复的请求"},
            {2, "执行失败"},
            {3, "错误的请求表单"},
            {4, "您的账户被停权，无法提问、抢答或重问，如有疑问请联系客服"},

            //SESSIONS
            {1001, "倒计时尚未结束，无法抢题"},
            {1002, "您已经抢到此题"},
            {1003, "此题已被其他人抢到"},
            {1004, "学生无法抢答"},
            {1005, "回答者无法评分"},
            {1006, "学生无法结束问题"},
            {1007, "此题目前不可抢答"},
            {1008, "提问时请提供题目描述或题目答案中的至少一项"},

            //USERS

            //SERVICES
            
            //ALERTS

            //SYNC
            {5001, "本地消息与服务器同步"},
            {5002, "本地消息与服务器不同步"},

            //PRESENTATIONS

            //EXTRA
            {99999, "Something is going very wrong if you see this message."}
        };

        //public static POIUIScheduler Scheduler { get; set; }
    }

}