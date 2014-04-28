using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

using System.Net;
using System.Threading.Tasks;
//using System.Net.Http;

using POILibCommunication;
using System.Web.Configuration;

namespace POIProxy
{
    public class POIProxyToWxApi
    {
        //private static string baseReqUrl = "http://www.qdaan.com/POIWebService-test/dnsServer/weixinApi.php";
        private static string baseReqUrl = WebConfigurationManager.AppSettings["WxServer"];

        private async static Task sendReq(NameValueCollection postVal)
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

        public static async Task interactiveSessionCreated(string userId, string sessionId)
        {
            NameValueCollection values = new NameValueCollection();
            values["userId"] = userId;
            values["sessionId"] = sessionId;

            values["reqType"] = "interactiveSessionCreated";

            await sendReq(values);
        }

        public static async Task interactiveSessionEnded(string userId, string sessionId)
        {
            NameValueCollection values = new NameValueCollection();
            values["userId"] = userId;
            values["sessionId"] = sessionId;

            values["reqType"] = "interactiveSessionEnded";

            await sendReq(values);
        }

        public static async Task interactiveSessionNewUserJoined(string userId, string sessionId, string userInfo)
        {
            NameValueCollection values = new NameValueCollection();
            values["userId"] = userId;
            values["sessionId"] = sessionId;
            values["userInfo"] = userInfo;

            values["reqType"] = "interactiveSessionNewUserJoined";

            await sendReq(values);
        }

        public static async Task interactiveSessionJoined(string userId, string sessionId)
        {
            NameValueCollection values = new NameValueCollection();
            values["userId"] = userId;
            values["sessionId"] = sessionId;

            values["reqType"] = "interactiveSessionJoined";

            await sendReq(values);
        }

        public static async Task interactiveSessionJoinFailed(string userId, string sessionId)
        {
            NameValueCollection values = new NameValueCollection();
            values["userId"] = userId;
            values["sessionId"] = sessionId;

            values["reqType"] = "interactiveSessionJoinFailed";

            await sendReq(values);
        }

        public static async Task interactiveSessionJoinBeforeTimeLimit(string userId, string sessionId)
        {
            NameValueCollection values = new NameValueCollection();
            values["userId"] = userId;
            values["sessionId"] = sessionId;

            values["reqType"] = "interactiveSessionJoinBeforeTimeLimit";

            await sendReq(values);
        }

        public static async Task interactiveSessionReraised(string userId, string sessionId, string newSessionId)
        {
            NameValueCollection values = new NameValueCollection();
            values["userId"] = userId;
            values["sessionId"] = sessionId;
            values["newSessionId"] = newSessionId;

            values["reqType"] = "interactiveSessionReraised";

            await sendReq(values);
        }

        public static async Task interactiveSessionCancelled(string userId, string sessionId)
        {
            NameValueCollection values = new NameValueCollection();
            values["userId"] = userId;
            values["sessionId"] = sessionId;

            values["reqType"] = "interactiveSessionCancelled";

            await sendReq(values);
        }

        private static async Task msgReceived(string userId, string sessionId, string msgType, string message, string mediaId)
        {
            NameValueCollection values = new NameValueCollection();
            values["userId"] = userId;
            values["sessionId"] = sessionId;
            values["msgType"] = msgType;
            values["message"] = message;
            values["mediaId"] = mediaId;

            values["reqType"] = "msgReceived";

            await sendReq(values);
        }

        public static async Task textMsgReceived(string userId, string sessionId, string message)
        {
            await msgReceived(userId, sessionId, "text", message, "");
        }

        public static async Task voiceMsgReceived(string userId, string sessionId, string mediaId)
        {
            await msgReceived(userId, sessionId, "voice", "", mediaId);
        }

        public static async Task imageMsgReceived(string userId, string sessionId, string mediaId)
        {
            await msgReceived(userId, sessionId, "image", "", mediaId);
        }

        public static async Task illustrationMsgReceived(string userId, string sessionId, string mediaId)
        {
            await msgReceived(userId, sessionId, "illustration", "", mediaId);
        }
    }
}