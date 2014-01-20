using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

using System.Net;
//using System.Net.Http;

using POILibCommunication;

namespace POIProxy
{
    public class POIProxyToWxApi
    {
        private static string baseReqUrl = "http://www.qdaan.com/POIWebService-test/dnsServer/weixinApi.php";

        private async static void sendReq(NameValueCollection postVal)
        {
            postVal["appProxy"] = "Yes";

            using (WebClient client = new WebClient())
            {
                client.Proxy = null;

                try
                {
                    await client.UploadValuesTaskAsync(baseReqUrl, postVal);
                }
                catch (WebException ex)
                {
                    POIGlobalVar.POIDebugLog(ex);
                }
            }
        }

        public static void interactiveSessionJoined(string userId, string sessionId)
        {
            NameValueCollection values = new NameValueCollection();
            values["userId"] = userId;
            values["sessionId"] = sessionId;

            values["reqType"] = "interactiveSessionJoined";

            sendReq(values);
        }

        private static void msgReceived(string userId, string sessionId, string msgType, string message, string mediaId)
        {
            NameValueCollection values = new NameValueCollection();
            values["userId"] = userId;
            values["sessionId"] = sessionId;
            values["msgType"] = msgType;
            values["message"] = message;
            values["mediaId"] = mediaId;

            values["reqType"] = "msgReceived";

            sendReq(values);
        }

        public static void textMsgReceived(string userId, string sessionId, string message)
        {
            msgReceived(userId, sessionId, "text", message, "");
        }

        public static void voiceMsgReceived(string userId, string sessionId, string mediaId)
        {
            msgReceived(userId, sessionId, "voice", "", mediaId);
        }

        public static void imageMsgReceived(string userId, string sessionId, string mediaId)
        {
            msgReceived(userId, sessionId, "image", "", mediaId);
        }

        public static void illustrationMsgReceived(string userId, string sessionId, string mediaId)
        {
            msgReceived(userId, sessionId, "illustration", "", mediaId);
        }
    }
}