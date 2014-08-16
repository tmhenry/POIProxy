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

        public enum resource { SESSIONS, MESSAGES, USERS, SERVICES };
        public enum sessionType { CREATE, JOIN, END, CANCEL, UPDATE, RERAISE, RATING };
        public enum messageType { TEXT, IMAGE, VOICE, ILLUSTRATION };
        public enum serviceType { SYSTEM, ACTION, NEWS, EXTRA };
        public enum userType { UPDATE };
        public enum errorCode
        {
            SUCCESS = 0,
            TIME_LIMITED = 1001,
            ALREADY_JOINED = 1002,
            TAKEN_BY_OTHERS = 1003,
            STUDENT_CANNOT_JOIN = 1004,
            TUTOR_CANNOT_RATING = 1005,
            STUDENT_CANNOT_END = 1006,
            SESSION_NOT_OPEN = 1007
        };

        //public static POIUIScheduler Scheduler { get; set; }
    }

}