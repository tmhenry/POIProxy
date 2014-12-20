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
using System.Web.Configuration;

namespace POIProxy
{
    public class POIProxyPushNotifier
    {

        private static String APPID = WebConfigurationManager.AppSettings["getuiAppId"];                            //您应用的AppId
        private static String APPKEY = WebConfigurationManager.AppSettings["getuiAppKey"];                          //您应用的AppKey
        private static String MASTERSECRET = WebConfigurationManager.AppSettings["getuiMasterSecret"];              //您应用的MasterSecret 
        private static String HOST = WebConfigurationManager.AppSettings["getuiHost"];                              //HOST：OpenService接口地址

        public static void send(List<string> userList, string message)
        {
            //detect is or not needed to push to app.
            List<com.igetui.api.openservice.igetui.Target> targetList = new List<com.igetui.api.openservice.igetui.Target>();
            JavaScriptSerializer jsonHandler = new JavaScriptSerializer();
            Dictionary<string, string> msgInfo = jsonHandler.Deserialize<Dictionary<string, string>>(message);
            bool needToPushApp = false;
            foreach (string userId in userList)
            {
                string system = POIProxySessionManager.Instance.getUserDevice(userId)["system"];
                if (system == "ios" || system == "android")
                {
                    needToPushApp = true;
                    com.igetui.api.openservice.igetui.Target target = new com.igetui.api.openservice.igetui.Target();
                    target.appId = APPID;
                    target.clientId = POIProxySessionManager.Instance.getUserDevice(userId)["deviceId"];
                    targetList.Add(target);

                    if (msgInfo.ContainsKey("sessionId"))
                    {
                        if (POIProxySessionManager.Instance.checkIsDeletedSession(msgInfo["sessionId"], userId)) {
                            return;
                        }
                    }
                }
                else
                {
                    continue;
                }
            }

            if (needToPushApp)
            {
                String title = "";
                switch (int.Parse(msgInfo["resource"]))
                {
                    case (int)POIGlobalVar.resource.SESSIONS:
                        if ((int)POIGlobalVar.sessionType.JOIN ==int.Parse( msgInfo["sessionType"]))
                        {
                            title = "有人来帮我解决问题啦!";
                        }
                        else if ((int)POIGlobalVar.sessionType.RERAISE == int.Parse(msgInfo["sessionType"]))
                        {
                            title = "很可惜,刚才的回答被小朋友重新提问了!";
                        }
                        else if ((int)POIGlobalVar.sessionType.RATING == int.Parse(msgInfo["sessionType"]))
                        {
                            title = "您正在回答的问题获得了评分!";
                        }
                        else if ((int)POIGlobalVar.sessionType.END == int.Parse(msgInfo["sessionType"]))
                        {
                            title = "您的问题已经被解决,请为辛苦的志愿者评分^_^";
                        }
                        else
                        {
                            title = "你收到了一条信息";
                        }
                        break;
                    case (int)POIGlobalVar.resource.MESSAGES:
                        if ((int)POIGlobalVar.messageType.TEXT == int.Parse(msgInfo["msgType"]))
                        {
                            title = "你收到了一条文字信息";
                        }
                        else if ((int)POIGlobalVar.messageType.VOICE == int.Parse(msgInfo["msgType"]))
                        {
                            title = "你收到了一条语音信息";
                        }
                        else if ((int)POIGlobalVar.messageType.IMAGE == int.Parse(msgInfo["msgType"]))
                        {
                            title = "你收到了一条图片信息";
                        }
                        else if ((int)POIGlobalVar.messageType.ILLUSTRATION == int.Parse(msgInfo["msgType"]))
                        {
                            title = "你收到了一条讲解信息";
                        }
                        else 
                        {
                            title = "你收到了一条信息";
                        }
                        break;
                    case (int)POIGlobalVar.resource.SERVICES:
                        if (msgInfo["title"] != "")
                        {
                            title = msgInfo["title"];
                        }
                        else 
                        {
                            title = "你收到了一条系统消息";
                        }
                        break;
                    default:
                        title = "新信息提醒";
                        break;
                }
                msgInfo["title"] = title;
                message = jsonHandler.Serialize(msgInfo);
                String pushResult = pushMessageToList(title, message, targetList);
                PPLog.infoLog("[POIProxyPushNotifier]  push to app result: " + pushResult);
            }

        }

        private static string pushMessageToList(string title, string message, List<com.igetui.api.openservice.igetui.Target> targetList)
        {
            TransmissionTemplate transMissionTemplate = transmissionTemplate(title, message);
            IGtPush push = new IGtPush(HOST, APPKEY, MASTERSECRET);
            ListMessage listMessage = new ListMessage();
            listMessage.IsOffline = true;                                // 用户当前不在线时，是否离线存储,可选
            listMessage.OfflineExpireTime = 1000 * 3600 * 24 * 10;            // 离线有效时间，单位为毫秒，可选
            listMessage.Data = transMissionTemplate;

            String contentId = push.getContentId(listMessage);
            String pushResult = push.pushMessageToList(contentId, targetList);
            return pushResult;
        }

        public static void broadcast(string pushMsg, string title = null)
        {
            if(title == null)
                title = "有新题目啦,我来抢答!";
            JavaScriptSerializer jsonHandler = new JavaScriptSerializer();
            Dictionary<string, string> msgInfo = jsonHandler.Deserialize<Dictionary<string, string>>(pushMsg);
            msgInfo["title"] = title;
            pushMsg = jsonHandler.Serialize(msgInfo);
            TransmissionTemplate template = transmissionTemplate(title, pushMsg);
            NotificationTemplate notificationMsg = notificationTemplate(pushMsg);

            AppMessage message = new AppMessage();
            message.IsOffline = true;                                       // 用户当前不在线时，是否离线存储,可选
            message.OfflineExpireTime = 1000 * 3600 * 24 * 10;              // 离线有效时间，单位为毫秒，可选
            message.Data = template;

            List<String> appIdList = new List<string>();
            appIdList.Add(APPID);

            List<String> phoneTypeList = new List<string>();    //通知接收者的手机操作系统类型
            if (WebConfigurationManager.AppSettings["ENV"] == "production") {
                phoneTypeList.Add("ANDROID");
            }
            //phoneTypeList.Add("IOS");

            List<String> provinceList = new List<string>();     //通知接收者所在省份
            //provinceList.Add("浙江");
            //provinceList.Add("上海");
            //provinceList.Add("北京");

            List<String> tagList = new List<string>();
            //tagList.Add("开心");

            message.AppIdList = appIdList;
            message.PhoneTypeList = phoneTypeList;
            message.ProvinceList = provinceList;
            message.TagList = tagList;

            IGtPush push = new IGtPush(HOST, APPKEY, MASTERSECRET);
            String androidPushResult = push.pushMessageToApp(message);

            List<string> deviceList = POIProxySessionManager.Instance.getDeviceBySystem("ios");
            /*List<com.igetui.api.openservice.igetui.Target> targetList = new List<com.igetui.api.openservice.igetui.Target>();
            foreach (string deviceId in deviceList)
            {
                com.igetui.api.openservice.igetui.Target target = new com.igetui.api.openservice.igetui.Target();
                target.appId = APPID;
                target.clientId = deviceId;
                targetList.Add(target);
            }
            String iOSPushResult = pushMessageToList(title, pushMsg, targetList);*/
            double deviceLength = Math.Ceiling((double)deviceList.Count / 1000);
            PPLog.debugLog("BroadCast: " + deviceLength);
            List<string> iOSPushResult = new List<string>();
            for (int i = 0; i < deviceLength; i++)
            {
                List<com.igetui.api.openservice.igetui.Target> targetList = new List<com.igetui.api.openservice.igetui.Target>();
                int targetCount = 1000;
                if (i == deviceLength - 1)
                {
                    targetCount = deviceList.Count % 1000;
                }

                for (int j = 0; j < targetCount; j++)
                {
                    com.igetui.api.openservice.igetui.Target target = new com.igetui.api.openservice.igetui.Target();
                    target.appId = APPID;
                    target.clientId = deviceList[(int)(1000 * i + j)];
                    targetList.Add(target);
                }
                if (WebConfigurationManager.AppSettings["ENV"] == "production") {
                    String result = pushMessageToList(title, pushMsg, targetList);
                    iOSPushResult.Add(result);
                }
            }
            PPLog.infoLog("[POIProxyPushNotifier] Session created broadcasted result: (android)" + androidPushResult + " (iOS) " + iOSPushResult);

            
        }

        public static TransmissionTemplate transmissionTemplate(String title, String message)
        {
            TransmissionTemplate template = new TransmissionTemplate();
            template.AppId = APPID;
            template.AppKey = APPKEY;
            template.TransmissionType = "2";            //应用启动类型，1：强制应用启动 2：等待应用启动
            template.TransmissionContent = message;  //透传内容
            //iOS推送需要的pushInfo字段
            //template.setPushInfo(actionLocKey, badge, message, sound, payload, locKey, locArgs, launchImage);
            template.setPushInfo("", 1, title, "", "", "", "", "");
            return template;
        }

        //通知透传模板动作内容
        public static NotificationTemplate notificationTemplate(String message)
        {
            NotificationTemplate template = new NotificationTemplate();
            template.AppId = APPID;
            template.AppKey = APPKEY;
            template.Title = "黄欢";         //通知栏标题
            template.Text = "黄欢";          //通知栏内容
            template.Logo = "";               //通知栏显示本地图片
            template.LogoURL = "";                    //通知栏显示网络图标

            template.TransmissionType = "1";          //应用启动类型，1：强制应用启动  2：等待应用启动
            template.TransmissionContent = message;   //透传内容
            //iOS推送需要的pushInfo字段
            //template.setPushInfo(actionLocKey, badge, message, sound, payload, locKey, locArgs, launchImage);
            template.setPushInfo("", 1, "新消息", "", "", "", "", "");

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