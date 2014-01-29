using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

using System.Net;
using POILibCommunication;
using System.Web.Script.Serialization;

namespace POIProxy
{
    public class POIProxyPushNotifier
    {
        private static string baseReqUrl = "http://www.qdaan.com/POIWebService-test/dnsServer/pushInterface.php";
        private static JavaScriptSerializer jsonHandler = new JavaScriptSerializer();

        //Note: user list needs to be json encoded list of user ids
        public async static void sendPushNotification(List<string> userList, string message)
        {
            NameValueCollection postVal = new NameValueCollection();
            postVal["userList"] = jsonHandler.Serialize(userList);
            postVal["message"] = message;

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

        public static void textMsgReceived(List<string> userList)
        {
            sendPushNotification(userList, "收到文字消息");
        }

        public static void imageMsgReceived(List<string> userList)
        {
            sendPushNotification(userList, "收到图片消息");
        }

        public static void voiceMsgReceived(List<string> userList)
        {
            sendPushNotification(userList, "收到语音消息");
        }

        public static void illustrationMsgReceived(List<string> userList)
        {
            sendPushNotification(userList, "收到白板演算消息");
        }
    }
}