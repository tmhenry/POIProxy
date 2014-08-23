using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace POIProxy.Models
{
    public class HomeViewModel
    {
        public string Name { get; set; }

        public string ContentServer { get; set; }
        public string DNSServer { get; set; }
        public string ProxyServerIP { get; set; }
        public int ProxyServerPort { get; set; }

        public string ProxyHost { get; set; }
        public int ProxyPort { get; set; }

        public int NumWebClients { get; set; }
        public int NumMobileClients { get; set; }
        public int NumOnlineSessions { get; set; }
    }
}