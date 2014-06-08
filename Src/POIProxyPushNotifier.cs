using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

using System.Net;
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
                //Send ios push
                var iosPush = new ParsePush();

                iosPush.Query = from installation in ParseInstallation.Query
                                where installation.DeviceType == "ios"
                                select installation;

                iosPush.Data = new Dictionary<string, object> {
                    {"alert", message},
                    {"sound", "cheering.caf"},
                    {"sessionId", sessionId},
                    {"action", "com.poi.login.HANDLE_NOTIFICATION"}
                };

                iosPush.Channels = new List<string> { "session_" + sessionId };

                await iosPush.SendAsync();
            }
            catch (Exception e)
            {
                PPLog.errorLog("Push to ios: " + e.Message);
            }

            try
            {
                //Send android push
                var androidPush = new ParsePush();

                androidPush.Query = from installation in ParseInstallation.Query
                                    where installation.DeviceType == "android"
                                    select installation;

                androidPush.Data = new Dictionary<string, object> {
                    {"message", message},
                    {"sessionId", sessionId},
                    {"action", "com.poi.login.HANDLE_NOTIFICATION"}
                };

                androidPush.Channels = new List<string> { "session_" + sessionId };

                await androidPush.SendAsync();
            }
            catch (Exception e)
            {
                PPLog.errorLog("Push to android: " + e.Message);
            }
            
        }

        public static async Task broadcastNotification(string sessionId, string message)
        {
            try
            {
                //Send ios push
                var iosPush = new ParsePush();

                iosPush.Query = from installation in ParseInstallation.Query
                                where installation.DeviceType == "ios"
                                select installation;

                iosPush.Data = new Dictionary<string, object> {
                    {"alert", message},
                    {"sound", "cheering.caf"},
                    {"sessionId", sessionId},
                    {"action", "com.poi.login.HANDLE_NOTIFICATION"}
                };

                iosPush.Channels = new List<string> { "broadcast" };

                await iosPush.SendAsync();
            }
            catch (Exception e)
            {
                PPLog.errorLog("Push to ios: " + e.Message);
            }

            try
            {
                //Send android push
                var androidPush = new ParsePush();

                androidPush.Query = from installation in ParseInstallation.Query
                                    where installation.DeviceType == "android"
                                    select installation;

                androidPush.Data = new Dictionary<string, object> {
                    {"message", message},
                    {"sessionId", sessionId},
                    {"action", "com.poi.login.HANDLE_NOTIFICATION"}
                };

                androidPush.Channels = new List<string> { "broadcast" };

                await androidPush.SendAsync();
            }
            catch (Exception e)
            {
                PPLog.errorLog("Push to android: " + e.Message);
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

        public static async Task sessionJoined(string sessionId)
        {
            await sendPushNotification(sessionId, "老师来了！");
        }

        public static async Task sessionRated(string sessionId, int rating)
        {
            await sendPushNotification(sessionId, "收到评分" + rating);
        }

        public static async Task sessionEnded(string sessionId)
        {
            await sendPushNotification(sessionId, "解答已经结束");
        }

        public static async Task sessionCreated(string sessionId)
        {
            PPLog.infoLog("[POIProxyPushNotifier sessionCreated] Session created broadcasted!");
            await broadcastNotification(sessionId, "有题了，快来抢！");
        }
    }
}