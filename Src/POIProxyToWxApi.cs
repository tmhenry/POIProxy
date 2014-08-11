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
            string userId = postVal["userId"];
            string system = POIProxySessionManager.getUserDevice(userId)["system"];
            if (system != "ios" && system != "android")
            {
                postVal["appProxy"] = "Yes";

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

                        PPLog.infoLog("[POIProxyToWxApi]  push to weixin result: " + responseStr);
                    }
                    catch (Exception e)
                    {
                        PPLog.errorLog(e.Message);
                    }

                }
            }
            else {
                //PPLog.infoLog("[POIProxyToWxApi sendReq] userId is not weixin User");
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

        private static async Task msgReceived(List<string> userList, string sessionId, string msgType, string message, string mediaId)
        {
            //the best way here is passing userList to weixin when multi weixin users join.so we don't consider here.
            foreach (string userId in userList) {
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