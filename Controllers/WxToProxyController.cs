﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

using System.Web.Script.Serialization;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace POIProxy.Controllers
{

    public class WxToProxyController : ApiController
    {
        POIProxyInteractiveMsgHandler interMsgHandler = POIGlobalVar.Kernel.myInterMsgHandler;
        JavaScriptSerializer jsonHandler = new JavaScriptSerializer();
        private string baseUrl = WebConfigurationManager.AppSettings["ProxyHost"] + ":" + WebConfigurationManager.AppSettings["ProxyPort"] + "/api/WxToProxy/";
        private enum resource { SESSIONS, MESSAGES, USERS, SERVICES};
        private enum sessionType { CREATE, JOIN, END, CANCEL, UPDATE, RERAISE, RATING};
        private enum messageType { TEXT, IMAGE, VOICE, ILLUSTRATION};
        private enum serviceType { SYSTEM, ACTION, NEWS, EXTRA };
        private enum userType { UPDATE };
        private enum errorCode { 
            SUCCESS = 0, 
            TIME_LIMITED = 1001, 
            ALREADY_JOINED = 1002, 
            TAKEN_BY_OTHERS = 1003, 
            STUDENT_CANNOT_JOIN = 1004,
            TUTOR_CANNOT_RATING = 1005,
            STUDENT_CANNOT_END = 1006,
            SESSION_NOT_OPEN = 1007 };

        [HttpPost]
        public HttpResponseMessage Post(HttpRequestMessage request)
        {
            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StringContent(jsonHandler.Serialize(new
            {
                post_message_url = baseUrl + "message",
                post_message_params = new { msgType = "text/image/voice/illustration", msgId = "uuid", sessionId = "sessionId", userId = "userId", message = "message", mediaId = "mediaId", timestamp = "timestamp" },
                post_session_url = baseUrl + "session",
                post_session_params = new { type = "createSession/joinSession/sessionEnded/sessionCancelled/sessionUpdated/reraiseSession/ratingReceived", msgId = "uuid", sessionId = "sessionId", userId = "userId" },
                post_user_url = baseUrl + "users",
                post_user_params = new { type = "updateUserDevice" }
            }));
            //response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
            return response;
        }

        [HttpPost]
        public async Task<HttpResponseMessage> Messages(HttpRequestMessage request)
        {
            //Check if post is coming from the allowed IP address
            try
            {
                //Check if post is coming from the allowed IP address
                string content = request.Content.ReadAsStringAsync().Result;
                Dictionary<string, string> msgInfo = jsonHandler.Deserialize<Dictionary<string, string>>(content);
                PPLog.infoLog("[ProxyController Messages] " + DictToString(msgInfo, null));

                string msgId = msgInfo["msgId"];
                string userId = msgInfo["userId"];
                string sessionId = msgInfo["sessionId"];
                int msgType = int.Parse(msgInfo["msgType"]);
                string message = msgInfo["message"];
                string mediaId = msgInfo["mediaId"];
                //double timestamp = double.Parse(msgInfo["timestamp"]);
                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                string pushMsg= jsonHandler.Serialize(new {
                    resource = resource.MESSAGES,
                    msgId = msgId,
                    userId = userId,
                    sessionId = sessionId,
                    msgType = msgType,
                    message = message,
                    mediaId = mediaId,
                    timestamp = timestamp
                });

                if (!POIProxySessionManager.checkEventExists(sessionId, msgId))
                {
                    List<string> userList = POIProxySessionManager.getUsersBySessionId(sessionId);
                    userList.Remove(userId);

                    switch (msgType)
                    {
                        case (int) messageType.TEXT:
                            interMsgHandler.textMsgReceived(msgId, userId, sessionId, message, timestamp);
                            POIProxyPushNotifier.send(userList, pushMsg);
                            await POIProxyToWxApi.textMsgReceived(userList, sessionId, message);
                            break;

                        case (int) messageType.IMAGE:
                            interMsgHandler.imageMsgReceived(msgId, userId, sessionId, mediaId, timestamp);
                            POIProxyPushNotifier.send(userList, pushMsg);
                            await POIProxyToWxApi.imageMsgReceived(userList, sessionId, mediaId);
                            break;

                        case (int) messageType.VOICE:
                            interMsgHandler.voiceMsgReceived(msgId, userId, sessionId, mediaId, timestamp);
                            POIProxyPushNotifier.send(userList, pushMsg);
                            await POIProxyToWxApi.voiceMsgReceived(userList, sessionId, mediaId);
                            break;

                        case (int) messageType.ILLUSTRATION:
                            interMsgHandler.illustrationMsgReceived(msgId, userId, sessionId, mediaId, timestamp);
                            POIProxyPushNotifier.send(userList, pushMsg);
                            await POIProxyToWxApi.illustrationMsgReceived(userList, sessionId, mediaId);
                            break;
                    }

                    var response = Request.CreateResponse(HttpStatusCode.OK);
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent(jsonHandler.Serialize(new { status = "success" }));
                    return response;
                }
                else 
                {
                    var response = Request.CreateResponse(HttpStatusCode.OK);
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent(jsonHandler.Serialize(new { status = "duplicated" }));
                    return response;
                }
                
            }
            catch (Exception e) 
            {
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.ExpectationFailed;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = "fail", content = e.Message }));
                return response;
            }
            
        }

        [HttpPost]
        public async Task<HttpResponseMessage> Sessions(HttpRequestMessage request)
        {
            try
            {
                string content = request.Content.ReadAsStringAsync().Result;
                Dictionary<string, string> msgInfo = jsonHandler.Deserialize<Dictionary<string, string>>(content);
                PPLog.infoLog("[ProxyController Sessions] " + DictToString(msgInfo, null));

                int type = int.Parse(msgInfo["type"]);
                string msgId = msgInfo["msgId"];
                string userId = msgInfo["userId"];
                string sessionId = msgInfo.ContainsKey("sessionId") ? msgInfo["sessionId"] : "";
                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                msgInfo["timestamp"] = timestamp.ToString();

                string desc, mediaId, returnContent = "";
                int rating = 0, errcode = 0;
                string pushMsg = jsonHandler.Serialize(new
                {
                    resource = resource.SESSIONS,
                    sessionType = type,
                    msgId = msgId,
                    userId = userId,
                    userInfo = jsonHandler.Serialize(POIProxySessionManager.getUserInfo(userId)),
                    sessionId = sessionId,
                    timestamp = timestamp,
                });

                if (!POIProxySessionManager.checkEventExists(sessionId, msgId))
                {
                    List<string> userList = POIProxySessionManager.getUsersBySessionId(sessionId);
                    userList.Remove(userId);
                    switch (type)
                    {
                        case (int)sessionType.RATING:
                            var sessionInfo = POIProxySessionManager.getSessionInfo(sessionId);
                            if (sessionInfo["creator"] != userId) 
                            {
                                errcode = (int)errorCode.TUTOR_CANNOT_RATING;
                                break;
                            }
                            rating = Convert.ToInt32(msgInfo["rating"]);
                            interMsgHandler.rateInteractiveSession(msgInfo);

                            Dictionary<string, string> pushDic = jsonHandler.Deserialize<Dictionary<string, string>>(pushMsg);
                            pushDic["rating"] = rating.ToString();
                            pushMsg = jsonHandler.Serialize(pushDic);
                            POIProxyPushNotifier.send(userList, pushMsg);
                            //need to write for weixin tutor notifier.

                            break;

                        case (int)sessionType.UPDATE:
                            desc = msgInfo["description"];
                            mediaId = msgInfo["mediaId"];

                            if (desc != "") interMsgHandler.updateQuestionDescription(sessionId, desc);
                            if (mediaId != "") interMsgHandler.updateQuestionMediaId(sessionId, mediaId);
                            break;

                        case (int)sessionType.CANCEL:
                            interMsgHandler.cancelInteractiveSession(msgInfo);
                            break;

                        case (int)sessionType.END:
                            sessionInfo = POIProxySessionManager.getSessionInfo(sessionId);
                            if (sessionInfo["creator"] == userId) 
                            {
                                errcode = (int)errorCode.STUDENT_CANNOT_END;
                                break;
                            }
                            interMsgHandler.endInteractiveSession(msgId, userId, sessionId);
                            sessionInfo = POIProxySessionManager.getSessionInfo(sessionId);
                            await POIProxyToWxApi.interactiveSessionEnded(sessionInfo["creator"], sessionId);
                            POIProxyPushNotifier.send(userList, pushMsg);

                            break;

                        //Join and create operation received from weixin 
                        case (int)sessionType.JOIN:
                            sessionId = msgInfo["sessionId"];
                            sessionInfo = POIProxySessionManager.getSessionInfo(sessionId);
                            errcode = await wxJoinInteractiveSession(msgInfo, sessionInfo, pushMsg, userList);
                            if(errcode == (int)errorCode.SUCCESS)
                                returnContent = jsonHandler.Serialize(POIProxySessionManager.getSessionArchive(sessionId));
                            break;

                        case (int)sessionType.CREATE:
                            Tuple<string, string> result = interMsgHandler.
                            createInteractiveSession(msgInfo["msgId"], msgInfo["userId"], msgInfo["mediaId"], msgInfo["description"], "private");
                            string presId = result.Item1;
                            sessionId = result.Item2;

                            broadCastCreateSession(sessionId, pushMsg);
                            returnContent = jsonHandler.Serialize(new { sessionId = sessionId, timestamp = timestamp });
                            break;

                        case (int)sessionType.RERAISE:
                            string newSessionId = interMsgHandler.duplicateInteractiveSession(sessionId, timestamp);
                            interMsgHandler.reraiseInteractiveSession(msgId, userId, sessionId, newSessionId, timestamp);
                            //Notify the student about interactive session reraised
                            await POIProxyToWxApi.interactiveSessionReraised(userId, sessionId, newSessionId);

                            POIProxyPushNotifier.send(userList, pushMsg);
                            broadCastCreateSession(newSessionId, pushMsg);
                            returnContent = jsonHandler.Serialize(new { sessionId = newSessionId, timestamp = timestamp });
                            break;
                    } 
                    var response = Request.CreateResponse(HttpStatusCode.OK);
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent(jsonHandler.Serialize(new { status = "success", type = type, errcode = errcode, content = returnContent }));
                    return response;
                }
                else
                {
                    var response = Request.CreateResponse(HttpStatusCode.OK);
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent(jsonHandler.Serialize(new { status = "duplicated" }));
                    return response;
                }
            }
            catch (Exception e)
            {
                PPLog.errorLog("In wx to proxy post session: " + e.Message);
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.ExpectationFailed;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = "fail", content = e.Message }));
                return response;
            }
            
        }


        [HttpPost]
        public HttpResponseMessage Users(HttpRequestMessage request)
        {
            string content = request.Content.ReadAsStringAsync().Result;
            Dictionary<string, string> userInfo = jsonHandler.Deserialize<Dictionary<string, string>>(content);
            PPLog.infoLog("[ProxyController users] " + DictToString(userInfo, null));

            int type = int.Parse(userInfo["type"]);
            try
            {
                switch (type)
                {
                    case (int) userType.UPDATE:
                        string deviceId = userInfo["deviceId"];
                        string userId = userInfo["userId"];
                        string system = userInfo["system"];
                        POIProxySessionManager.updateUserDevice(userId, deviceId, system);
                    break;
                }
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = "success" }));
                return response;
            }
            catch (Exception e)
            {
                PPLog.errorLog("error in users operation received: " + e.Message);
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.ExpectationFailed;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = "fail", content = e.Message }));
                return response;
            }
        }

        public HttpResponseMessage Services(HttpRequestMessage request)
        {
            try
            {
                string content = request.Content.ReadAsStringAsync().Result;
                Dictionary<string, string> serviceInfo = jsonHandler.Deserialize<Dictionary<string, string>>(content);
                PPLog.infoLog("[ProxyController Sessions] " + DictToString(serviceInfo, null));

                string serviceType = serviceInfo.ContainsKey("serviceType") ? serviceInfo["serviceType"] : "";
                string msgId = serviceInfo.ContainsKey("msgId") ? serviceInfo["msgId"] : "";
                string title = serviceInfo.ContainsKey("title") ? serviceInfo["title"] : "";
                string mediaId = serviceInfo.ContainsKey("mediaId") ? serviceInfo["mediaId"] : "";
                string url = serviceInfo.ContainsKey("url") ? serviceInfo["url"] : "";
                string message = serviceInfo.ContainsKey("message") ? serviceInfo["message"] : "";
                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                string pushMsg = jsonHandler.Serialize(new
                {
                    resource = resource.SERVICES,
                    serviceType = serviceType,
                    msgId = msgId,
                    title = title,
                    message = message,
                    mediaId = mediaId,
                    url = url,
                    timestamp = timestamp,
                });
                POIProxyPushNotifier.broadcast(pushMsg);
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = "success", type = resource.SERVICES }));
                return response;
            }
            catch (Exception e)
            {
                PPLog.errorLog("error in users operation received: " + e.Message);
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.ExpectationFailed;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = "fail", content = e.Message }));
                return response;
            }
        }


        private async Task<int> wxJoinInteractiveSession(Dictionary<string, string> msgInfo, Dictionary<string, string> sessionInfo, string pushMsg, List<string> userList)
        {
            string msgId = msgInfo["msgId"];
            string sessionId = msgInfo["sessionId"];
            string userId = msgInfo["userId"];
            var userInfo = POIProxySessionManager.getUserInfo(userId);
            string userInfoJson = jsonHandler.Serialize(userInfo);

            /*if (double.Parse(sessionInfo["create_at"])
                >= POITimestamp.ConvertToUnixTimestamp(DateTime.Now.AddSeconds(-60)))
            {
                PPLog.infoLog("Cannot join the session, not passing time limit");
                //Notify the weixin user about the join failed
                await POIProxyToWxApi.interactiveSessionJoinBeforeTimeLimit(userId, sessionId);
                return (int) errorCode.TIME_LIMITED;
            }
            else */if (!interMsgHandler.checkSessionOpen(sessionId))
            {
                PPLog.infoLog("[POIProxyHub wxJoinInteractiveSession] session status is not open");
                return (int)errorCode.SESSION_NOT_OPEN;
            }
            else if (POIProxySessionManager.checkUserInSession(sessionId, userId))
            {
                //User already in the session
                PPLog.infoLog("[POIProxyHub wxJoinInteractiveSession] Session already joined");

                //Notify the weixin users about the join operation
                await POIProxyToWxApi.interactiveSessionJoined(userId, sessionId);
                return (int) errorCode.ALREADY_JOINED;
            }
            else if (userInfo["accessRight"] != "tutor") {
                PPLog.infoLog("[POIProxyHub wxJoinInteractiveSession] forbidden join");
                return (int)errorCode.STUDENT_CANNOT_JOIN;
            }
            else if (POIProxySessionManager.acquireSessionToken(sessionId))
            {
                PPLog.infoLog("[POIProxyHub wxJoinInteractiveSession] Session is open, joined!");
                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                interMsgHandler.joinInteractiveSession(msgId, userId, sessionId, timestamp);

                POIProxySessionManager.subscribeSession(sessionId, userId);

                //Notify the weixin users about the join operation
                await POIProxyToWxApi.interactiveSessionJoined(userId, sessionId);

                //Notify the wexin server about the join operation
                await POIProxyToWxApi.interactiveSessionNewUserJoined(sessionInfo["creator"], sessionId, userInfoJson);

                //Send push notification
                POIProxyPushNotifier.send(userList, pushMsg);
                return 0;
            }
            else
            {
                PPLog.infoLog("[POIProxyHub wxJoinInteractiveSession] Cannot join the session, taken by others");
                //Notify the weixin user about the join failed
                await POIProxyToWxApi.interactiveSessionJoinFailed(userId, sessionId);
                return (int)errorCode.TAKEN_BY_OTHERS;
            }
        }

        private void broadCastCreateSession(string sessionId, string pushMsg)
        {
            Dictionary<string, object> tempDic = jsonHandler.Deserialize<Dictionary<string, object>>(pushMsg);
            tempDic["sessionType"] = sessionType.CREATE;
            tempDic["sessionId"] = sessionId;
            pushMsg = jsonHandler.Serialize(tempDic);

            POIProxyPushNotifier.broadcast(pushMsg);
        }

        public string DictToString<T, V>(IEnumerable<KeyValuePair<T, V>> items, string format)
        {
            format = String.IsNullOrEmpty(format) ? "{0}='{1}' " : format;

            System.Text.StringBuilder itemString = new System.Text.StringBuilder();
            foreach (var item in items)
                itemString.AppendFormat(format, item.Key, item.Value);

            return itemString.ToString();
        }
    }
}