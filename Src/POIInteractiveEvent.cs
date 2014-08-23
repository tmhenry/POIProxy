using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace POIProxy
{
    public class POIInteractiveEvent
    {
        public string EventType { get; set; }
        public string EventId { get; set; }
        public string MediaId { get; set; }
        public string Message { get; set; }
        public string UserId { get; set; }
        public double Timestamp { get; set; }
        public float MediaDuration { get; set; }

        //Additional data for special events
        public Dictionary<string, string> Data { get; set; }
    }
}