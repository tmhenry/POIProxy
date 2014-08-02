using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

using System.Net;
using System.Web.Script.Serialization;

using System.Threading.Tasks;
using Parse;
using com.igetui.api.openservice;
using com.igetui.api.openservice.igetui;
using com.igetui.api.openservice.igetui.template;

namespace POIProxy
{
    public class POIProxyPushNotifier
    {

        private static String APPID = "g5NzYPihq79hDqeEniKdB4";                     //您应用的AppId
        private static String APPKEY = "14fWxOQdCG6wxzXPGqTwA6";                    //您应用的AppKey
        private static String MASTERSECRET = "6X52s1s6KiAlySbI7STBn9";              //您应用的MasterSecret 
        private static String CLIENTID = "3f2e7654c014e39a53c05fc22ff8bc359b67026d5525fe85e51b7460b76a139c";        //您获取的clientID
        private static String HOST = "http://sdk.open.api.igexin.com/apiex.htm";    //HOST：OpenService接口地址

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

        public static async Task textMsgReceived(List<string> userId, string sessionId, string message, double timestamp)
        {
            //await sendPushNotification(sessionId, "收到文字消息");
            sendPushNotificationTest(userId, sessionId, message, timestamp);
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

        public static void sendPushNotificationTest(List<string> userList, string sessionId, string message, double timestamp)
        {
            TransmissionTemplate transMissionTemplate = TransmissionTemplate(message);

            NotificationTemplate notifactionTemplate = NotificationTemplate(message);

            IGtPush push = new IGtPush(HOST, APPKEY, MASTERSECRET);
            ListMessage listMessage = new ListMessage();
            listMessage.IsOffline = true;                         // 用户当前不在线时，是否离线存储,可选
            listMessage.OfflineExpireTime = 1000 * 3600 * 12;            // 离线有效时间，单位为毫秒，可选
            listMessage.Data = transMissionTemplate;

            List<com.igetui.api.openservice.igetui.Target> targetList = new List<com.igetui.api.openservice.igetui.Target>();
            foreach (string userId in userList) {
                com.igetui.api.openservice.igetui.Target target = new com.igetui.api.openservice.igetui.Target();
                target.appId = APPID;

                target.clientId = POIProxySessionManager.getUserDevice(userId);
                //target.clientId = "0e31593baa61f992627c340b61d74996";
                targetList.Add(target);
            }

            String contentId = push.getContentId(listMessage);
            String pushResult = push.pushMessageToList(contentId, targetList);
            PPLog.debugLog("push result：" + pushResult);
        }

        public static TransmissionTemplate TransmissionTemplate(String message)
        {
            TransmissionTemplate template = new TransmissionTemplate();
            template.AppId = APPID;
            template.AppKey = APPKEY;
            template.TransmissionType = "2";            //应用启动类型，1：强制应用启动 2：等待应用启动
            template.TransmissionContent = message;  //透传内容
            //iOS推送需要的pushInfo字段
            //template.setPushInfo(actionLocKey, badge, message, sound, payload, locKey, locArgs, launchImage);
            //template.setPushInfo("", 4, "", "", "", "", "", "");
            return template;
        }

        //通知透传模板动作内容
        public static NotificationTemplate NotificationTemplate(String message)
        {
            NotificationTemplate template = new NotificationTemplate();
            template.AppId = APPID;
            template.AppKey = APPKEY;
            template.Title = "优问答文字消息";         //通知栏标题
            template.Text = message;          //通知栏内容
            template.Logo = "";               //通知栏显示本地图片
            template.LogoURL = "";                    //通知栏显示网络图标

            template.TransmissionType = "1";          //应用启动类型，1：强制应用启动  2：等待应用启动
            template.TransmissionContent = "sessionId:1100";   //透传内容
            //iOS推送需要的pushInfo字段
            //template.setPushInfo(actionLocKey, badge, message, sound, payload, locKey, locArgs, launchImage);

            template.IsRing = true;                //接收到消息是否响铃，true：响铃 false：不响铃
            template.IsVibrate = true;               //接收到消息是否震动，true：震动 false：不震动
            template.IsClearable = true;             //接收到消息是否可清除，true：可清除 false：不可清除
            return template;
        }

        public static LinkTemplate LinkTemplateDemo()
        {
            LinkTemplate template = new LinkTemplate();
            template.AppId = APPID;
            template.AppKey = APPKEY;
            template.Title = "收到文字消息";         //通知栏标题
            template.Text = "成功啦!";          //通知栏内容
            template.Logo = "";               //通知栏显示本地图片
            template.LogoURL = "";  //通知栏显示网络图标，如无法读取，则显示本地默认图标，可为空
            template.Url = "http://www.baidu.com";      //打开的链接地址

            //iOS推送需要的pushInfo字段
            //template.setPushInfo(actionLocKey, badge, message, sound, payload, locKey, locArgs, launchImage);

            template.IsRing = true;                 //接收到消息是否响铃，true：响铃 false：不响铃
            template.IsVibrate = true;               //接收到消息是否震动，true：震动 false：不震动
            template.IsClearable = true;             //接收到消息是否可清除，true：可清除 false：不可清除

            return template;
        }

        public static NotyPopLoadTemplate NotyPopLoadTemplateDemo()
        {
            NotyPopLoadTemplate template = new NotyPopLoadTemplate();
            template.AppId = APPID;
            template.AppKey = APPKEY;
            template.NotyTitle = "测试标题";     //通知栏标题
            template.NotyContent = "测试内容";   //通知栏内容
            template.NotyIcon = "icon.png";           //通知栏显示本地图片
            template.LogoURL = "http://www-igexin.qiniudn.com/wp-content/uploads/2013/08/logo_getui1.png";                    //通知栏显示网络图标

            template.PopTitle = "弹框标题";             //弹框显示标题
            template.PopContent = "弹框内容";           //弹框显示内容
            template.PopImage = "";                     //弹框显示图片
            template.PopButton1 = "下载";               //弹框左边按钮显示文本
            template.PopButton2 = "取消";               //弹框右边按钮显示文本

            template.LoadTitle = "下载标题";            //通知栏显示下载标题
            template.LoadIcon = "file://push.png";      //通知栏显示下载图标,可为空
            template.LoadUrl = "http://www.appchina.com/market/d/425201/cop.baidu_0/com.gexin.im.apk";//下载地址，不可为空

            template.IsActived = true;                  //应用安装完成后，是否自动启动
            template.IsAutoInstall = true;              //下载应用完成后，是否弹出安装界面，true：弹出安装界面，false：手动点击弹出安装界面

            template.IsBelled = true;                 //接收到消息是否响铃，true：响铃 false：不响铃
            template.IsVibrationed = true;               //接收到消息是否震动，true：震动 false：不震动
            template.IsCleared = true;             //接收到消息是否可清除，true：可清除 false：不可清除
            return template;
        }
    }
}