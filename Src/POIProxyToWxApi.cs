using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
//using System.Net.Http;

using System.Web.Configuration;

namespace POIProxy
{
    public class POIProxyToWxApi
    {
        private static string baseReqUrl = WebConfigurationManager.AppSettings["WxServer"];

        private async static Task sendReq(NameValueCollection postVal)
        {
            PPLog.infoLog("[POIProxyToWxApi sendReq] WxApi.php base req url is: " + baseReqUrl);
            postVal["appProxy"] = "Yes";

            /*
            using (WebClient client = new WebClient())
            {
                client.Proxy = null;

                try
                {
                    PPLog.infoLog("post val is : " + postVal);
                    await client.UploadValuesTaskAsync(baseReqUrl, postVal);
                }
                catch (WebException ex)
                {
                    PPLog.infoLog(ex);
                }
            }*/

            using (var client = new HttpClient())
            {
                var values = new List<KeyValuePair<string, string>>();
                foreach (string key in postVal.Keys)
                {
                    values.Add(new KeyValuePair<string, string>(key, postVal[key]));
                }

                try
                {
                    var content = new FormUrlEncodedContent(values);
                    var response = await client.PostAsync(baseReqUrl, content);

                    var responseStr = await response.Content.ReadAsStringAsync();

                    PPLog.infoLog("[POIProxyToWxApi sendReq] responseStr:" + responseStr);
                }
                catch(Exception e)
                {
                    PPLog.errorLog(e.Message);
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
            PPLog.infoLog("Sending new user joined message to wxApi.php");

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

        private static async Task msgReceived(List<string> userList, string sessionId, string msgType, string message, string mediaId)
        {
            //the best way here is passing userList to weixin when multi weixin users join.so we don't consider here.
            foreach (string userId in userList) {
                string system = POIProxySessionManager.getUserDevice(userId)["system"];
                if (system == "weixin") {
                    NameValueCollection values = new NameValueCollection();
                    values["userId"] = userId;
                    values["sessionId"] = sessionId;
                    values["msgType"] = msgType;
                    values["message"] = message;
                    values["mediaId"] = mediaId;

                    values["reqType"] = "msgReceived";

                    await sendReq(values);
                }
            }
        }

        public static async Task textMsgReceived(List<string> userList, string sessionId, string message)
        {
            await msgReceived(userList, sessionId, "text", message, "");
        }

        public static async Task voiceMsgReceived(List<string> userList, string sessionId, string mediaId)
        {
            await msgReceived(userList, sessionId, "voice", "", mediaId);
        }

        public static async Task imageMsgReceived(List<string> userList, string sessionId, string mediaId)
        {
            await msgReceived(userList, sessionId, "image", "", mediaId);
        }

        public static async Task illustrationMsgReceived(List<string> userList, string sessionId, string mediaId)
        {
            await msgReceived(userList, sessionId, "illustration", "", mediaId);
        }
    }
}