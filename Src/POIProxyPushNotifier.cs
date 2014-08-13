﻿using System;
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
            bool needToPushApp = false;
            foreach (string userId in userList)
            {
                string system = POIProxySessionManager.getUserDevice(userId)["system"];
                if (system == "ios" || system == "android")
                {
                    needToPushApp = true;
                    com.igetui.api.openservice.igetui.Target target = new com.igetui.api.openservice.igetui.Target();
                    target.appId = APPID;
                    target.clientId = POIProxySessionManager.getUserDevice(userId)["deviceId"];
                    targetList.Add(target);
                }
                else
                {
                    continue;
                }
            }

            if (needToPushApp)
            {
                TransmissionTemplate transMissionTemplate = transmissionTemplate(message);

                IGtPush push = new IGtPush(HOST, APPKEY, MASTERSECRET);
                ListMessage listMessage = new ListMessage();
                listMessage.IsOffline = true;                                // 用户当前不在线时，是否离线存储,可选
                listMessage.OfflineExpireTime = 1000 * 3600 * 24 * 10;            // 离线有效时间，单位为毫秒，可选
                listMessage.Data = transMissionTemplate;

                String contentId = push.getContentId(listMessage);
                String pushResult = push.pushMessageToList(contentId, targetList);
                PPLog.infoLog("[POIProxyPushNotifier]  push to app result: " + pushResult);
            }

        }

        public static void broadcast(string pushMsg)
        {
            TransmissionTemplate template = transmissionTemplate(pushMsg);
            NotificationTemplate notificationMsg = notificationTemplate(pushMsg);

            AppMessage message = new AppMessage();
            message.IsOffline = true;                                       // 用户当前不在线时，是否离线存储,可选
            message.OfflineExpireTime = 1000 * 3600 * 24 * 10;              // 离线有效时间，单位为毫秒，可选
            message.Data = template;

            List<String> appIdList = new List<string>();
            appIdList.Add(APPID);

            List<String> phoneTypeList = new List<string>();    //通知接收者的手机操作系统类型
            phoneTypeList.Add("ANDROID");
            phoneTypeList.Add("IOS");

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
            String pushResult = push.pushMessageToApp(message);
            PPLog.infoLog("[POIProxyPushNotifier] Session created broadcasted result: " + pushResult);
        }

        public static TransmissionTemplate transmissionTemplate(String message)
        {
            TransmissionTemplate template = new TransmissionTemplate();
            template.AppId = APPID;
            template.AppKey = APPKEY;
            template.TransmissionType = "2";            //应用启动类型，1：强制应用启动 2：等待应用启动
            template.TransmissionContent = message;  //透传内容
            //iOS推送需要的pushInfo字段
            //template.setPushInfo(actionLocKey, badge, message, sound, payload, locKey, locArgs, launchImage);
            template.setPushInfo("", 1, "新题目提醒", "", "", "", "", "");
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