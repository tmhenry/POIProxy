using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace POIProxy
{
    public static class POIProxyGlobalVar
    {
        public static string MainUrl { get; set; }
        public static string SignalRUrl { get; set; }
        public static string AudioStreamingUrl { get { return @"http://192.168.1.143/"; } }
        public static POIProxyKernel Kernel { get; set; }
    }
}
