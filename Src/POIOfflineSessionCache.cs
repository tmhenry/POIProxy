using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;

namespace POIProxy
{
    public class POIOfflineSessionCache
    {
        Dictionary<Tuple<int, int>, Tuple<POIPresentation, POIMetadataArchive>> offlineSessionCache
            = new Dictionary<Tuple<int, int>, Tuple<POIPresentation, POIMetadataArchive>>();
        const int CacheSize = 100;

        public Tuple<POIPresentation, POIMetadataArchive> SearchSessionInfoInCache(int presId, int sessionId)
        {
            Tuple<int, int> key = new Tuple<int, int>(presId, sessionId);
            if (offlineSessionCache.ContainsKey(key))
            {
                return offlineSessionCache[key];
            }
            else
            {
                return null;
            }
        }

        public void AddRecordToSessionCache(int presId, int sessionId, POIPresentation pres, POIMetadataArchive archive)
        {
            Tuple<int, int> key = new Tuple<int, int>(presId, sessionId);
            if (!offlineSessionCache.ContainsKey(key))
            {
                Tuple<POIPresentation, POIMetadataArchive> val = new Tuple<POIPresentation, POIMetadataArchive>(pres, archive);
                offlineSessionCache.Add(key, val);
            }
        }
    }
}
