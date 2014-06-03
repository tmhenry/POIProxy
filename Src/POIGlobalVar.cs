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

        public static String KeywordsFileName { get { return "POI_Keywords.txt"; } }
        public static String KeywordsFileType { get { return ".txt"; } }

        public static int MaxMobileClientCount { get; set; }

        //public static POIUIScheduler Scheduler { get; set; }
    }

}