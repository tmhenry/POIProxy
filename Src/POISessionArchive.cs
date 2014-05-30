using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace POIProxy
{
    public class POISessionArchive
    {
        public string SessionId { get; set; }
        public Dictionary<string, string> Info { get; set; }
        public List<Dictionary<string,string>> UserList { get; set; }
        public List<POIInteractiveEvent> EventList { get; set; }
    }
}