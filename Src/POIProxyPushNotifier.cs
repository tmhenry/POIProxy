using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

using System.Net;
using POILibCommunication;
using System.Web.Script.Serialization;

using System.Threading.Tasks;
using Parse;

namespace POIProxy
{
    public class POIProxyPushNotifier
    {
        //Note: user list needs to be json encoded list of user ids
        public async static Task sendPushNotification(string sessionId, string message)
        {
            try
            {
                var push = new ParsePush();

                push.Data = new Dictionary<string, object> {
                    {"alert", message},
                    {"sessionId", sessionId},
                    {"action", "com.poi.login.HANDLE_NOTIFICATION"}
                };

                push.Channels = new List<string> { "session_" + sessionId };

                await push.SendAsync();
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog(e.Message);
            }
            
        }

        public static async Task textMsgReceived(string sessionId)
        {
            await sendPushNotification(sessionId, "收到文字消息");
        }

        public static async Task imageMsgReceived(string sessionId)
        {
            await sendPushNotification(sessionId, "收到图片消息");
        }

        public static async Task voiceMsgReceived(string sessionId)
        {
            await sendPushNotification(sessionId, "收到语音消息");
        }

        public static async Task illustrationMsgReceived(string sessionId)
        {
            await sendPushNotification(sessionId, "收到白板演算消息");
        }
    }
}