using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

using System.Net;
using POILibCommunication;
using System.Web.Script.Serialization;

using System.Threading.Tasks;

namespace POIProxy
{
    public class POIProxyPushNotifier
    {
        private static string baseReqUrl = "http://www.qdaan.com/POIWebService-test/dnsServer/pushInterface.php";
        private static JavaScriptSerializer jsonHandler = new JavaScriptSerializer();

        //Note: user list needs to be json encoded list of user ids
        public async static Task sendPushNotification(List<string> userList, string message)
        {
            NameValueCollection postVal = new NameValueCollection();
            postVal["userList"] = jsonHandler.Serialize(userList);
            postVal["message"] = message;

            //POIGlobalVar.POIDebugLog(postVal["userList"]);
            //POIGlobalVar.POIDebugLog(postVal["message"]);


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

        public static async Task textMsgReceived(List<string> userList)
        {
            await sendPushNotification(userList, "收到文字消息");
        }

        public static async Task imageMsgReceived(List<string> userList)
        {
            await sendPushNotification(userList, "收到图片消息");
        }

        public static async Task voiceMsgReceived(List<string> userList)
        {
            await sendPushNotification(userList, "收到语音消息");
        }

        public static async Task illustrationMsgReceived(List<string> userList)
        {
            await sendPushNotification(userList, "收到白板演算消息");
        }
    }
}