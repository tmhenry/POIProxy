using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

using System.Web.Script.Serialization;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Collections;

namespace POIProxy.Controllers
{

    public class WxToProxyController : ApiController
    {
        POIProxyInteractiveMsgHandler interMsgHandler = POIGlobalVar.Kernel.myInterMsgHandler;
        JavaScriptSerializer jsonHandler = new JavaScriptSerializer();
        private string baseUrl = WebConfigurationManager.AppSettings["ProxyHost"] + ":" + WebConfigurationManager.AppSettings["ProxyPort"] + "/api/WxToProxy/";

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
                string message = msgInfo.ContainsKey("message") ? msgInfo["message"] : "";
                string mediaId = msgInfo.ContainsKey("mediaId") ? msgInfo["mediaId"] : "";
                float mediaDuration = msgInfo.ContainsKey("mediaDuration") ? float.Parse(msgInfo["mediaDuration"]) : 0;
                string customerId = msgInfo.ContainsKey("customerId") ? msgInfo["customerId"] : POIGlobalVar.customerId;
                //double timestamp = double.Parse(msgInfo["timestamp"]);
                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                string pushMsg= jsonHandler.Serialize(new {
                    resource = POIGlobalVar.resource.MESSAGES,
                    msgId = msgId,
                    userId = userId,
                    sessionId = sessionId,
                    msgType = msgType,
                    message = message,
                    mediaId = mediaId,
                    mediaDuration = mediaDuration,
                    timestamp = timestamp
                });

                if (!POIProxySessionManager.Instance.checkEventExists(sessionId, msgId))
                {
                    List<string> userList = new List<string>();
                    if (sessionId != POIGlobalVar.customerSession.ToString())
                    {
                        userList = POIProxySessionManager.Instance.getUsersBySessionId(sessionId);
                        userList.Remove(userId);
                    }
                    else 
                    {
                        userList.Add(customerId);
                    }

                    switch (msgType)
                    {
                        case (int)POIGlobalVar.messageType.TEXT:
                            interMsgHandler.textMsgReceived(msgId, userId, sessionId, message, timestamp, customerId);
                            POIProxyPushNotifier.send(userList, pushMsg);
                            await POIProxyToWxApi.textMsgReceived(userList, sessionId, message);
                            break;

                        case (int)POIGlobalVar.messageType.IMAGE:
                            interMsgHandler.imageMsgReceived(msgId, userId, sessionId, mediaId, timestamp, customerId);
                            POIProxyPushNotifier.send(userList, pushMsg);
                            await POIProxyToWxApi.imageMsgReceived(userList, sessionId, mediaId);
                            break;

                        case (int)POIGlobalVar.messageType.VOICE:
                            interMsgHandler.voiceMsgReceived(msgId, userId, sessionId, mediaId, timestamp, mediaDuration, customerId);
                            POIProxyPushNotifier.send(userList, pushMsg);
                            await POIProxyToWxApi.voiceMsgReceived(userList, sessionId, mediaId);
                            break;

                        case (int)POIGlobalVar.messageType.ILLUSTRATION:
                            interMsgHandler.illustrationMsgReceived(msgId, userId, sessionId, mediaId, timestamp, customerId);
                            POIProxyPushNotifier.send(userList, pushMsg);
                            await POIProxyToWxApi.illustrationMsgReceived(userList, sessionId, mediaId);
                            break;

                        case (int)POIGlobalVar.messageType.SYSTEM:
                            interMsgHandler.systemMsgReceived(msgId, userId, sessionId, message, timestamp);
                            POIProxyPushNotifier.send(userList, pushMsg);
                            await POIProxyToWxApi.textMsgReceived(userList, sessionId, message);
                            break;

                        default:
                            break;
                    }

                    var response = Request.CreateResponse(HttpStatusCode.OK);
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.SUCCESS }));
                    return response;
                }
                else 
                {
                    var response = Request.CreateResponse(HttpStatusCode.OK);
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.DUPLICATED }));
                    return response;
                }
                
            }
            catch (Exception e) 
            {
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.ExpectationFailed;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.FAIL, content = e.Message }));
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

                string desc, mediaId = "";
                int rating = 0, errcode = 0;

                int returnStatus = 0;
                string returnErrMsg = "";
                string returnContent = "";
                var response = Request.CreateResponse(HttpStatusCode.OK);

                string pushMsg = jsonHandler.Serialize(new
                {
                    resource = POIGlobalVar.resource.SESSIONS,
                    msgType = POIGlobalVar.messageType.SYSTEM,
                    msgId = msgId,
                    userId = userId,
                    userInfo = jsonHandler.Serialize(POIProxySessionManager.Instance.getUserInfo(userId)),
                    sessionId = sessionId,
                    timestamp = timestamp,
                });

                if (POIProxySessionManager.Instance.checkEventExists(sessionId, msgId))
                {
                    errcode = (int)POIGlobalVar.errorCode.DUPLICATED;

                    returnStatus = (int)POIGlobalVar.errorCode.DUPLICATED;
                    returnErrMsg = "";
                    returnContent = "";

                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent(jsonHandler.Serialize(new { status = returnStatus, type = type, errcode = errcode, errMsg = returnErrMsg, content = returnContent }));
                    return response;

                }

                List<string> userList = POIProxySessionManager.Instance.getUsersBySessionId(sessionId);
                userList.Remove(userId);

                switch (type)
                {
                    case (int)POIGlobalVar.sessionType.CREATE:
                        if (POIProxySessionManager.Instance.checkDuplicatedCreatedSession(msgId))
                        {
                            sessionId = POIProxySessionManager.Instance.getSessionByMsgId(msgId);
                            returnStatus = (int)POIGlobalVar.errorCode.DUPLICATED;
                            returnErrMsg = "";
                            returnContent = jsonHandler.Serialize(new { sessionId = sessionId, timestamp = timestamp });
                            break;
                        }

                        if (POIProxySessionManager.checkUserBanList(userId))
                        {
                            returnStatus = (int)POIGlobalVar.errorCode.FAIL;
                            returnErrMsg = "您的账户被停权，无法提问，如有疑问请联系客服";
                            returnContent = "";
                            errcode = (int)POIGlobalVar.errorCode.FAIL;

                            string alertMsg = jsonHandler.Serialize(new
                            {
                                resource = POIGlobalVar.resource.ALERTS,
                                alertType = POIGlobalVar.alertType.SYSTEM,
                                //msgId = msgId,
                                title = "用户停权",
                                message = "您的账户被停权，无法提问，如有疑问请联系客服",
                                timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now),
                            });

                            if (userId != "")
                            {
                                List<string> pushList = new List<string>();
                                pushList.Add(userId);
                                POIProxyPushNotifier.send(pushList, alertMsg);
                            }

                            var responseErr = Request.CreateResponse(HttpStatusCode.OK);
                            responseErr.StatusCode = HttpStatusCode.ExpectationFailed;
                            responseErr.Content = new StringContent(jsonHandler.Serialize(new { status = returnStatus, type = type, errcode = errcode, errMsg = returnErrMsg, content = returnContent }));
                            return responseErr;
                        }

                        Tuple<string, string> result = interMsgHandler.
                            createInteractiveSession(msgId, userId, msgInfo["mediaId"], msgInfo["description"], "private", msgInfo.ContainsKey("filter") ? msgInfo["filter"] : "");
                        string presId = result.Item1;
                        sessionId = result.Item2;

                        if (WebConfigurationManager.AppSettings["ENV"] == "production")
                        {
                            broadCastCreateSession(sessionId, pushMsg);
                        }

                        returnStatus = (int)POIGlobalVar.errorCode.SUCCESS;
                        returnErrMsg = "";
                        returnContent = jsonHandler.Serialize(new { sessionId = sessionId, presId = presId, timestamp = timestamp });
 
                        break;

                    case (int)POIGlobalVar.sessionType.JOIN:
                    case (int)POIGlobalVar.sessionType.END:
                    case (int)POIGlobalVar.sessionType.CANCEL:
                    case (int)POIGlobalVar.sessionType.RATING:
                    case (int)POIGlobalVar.sessionType.RERAISE:

                        returnStatus = (int)POIGlobalVar.errorCode.FAIL;
                        returnErrMsg = "由于最新版本中问答系统进行了重大调整，一些旧版功能已不被支持，请及时更新版本，谢谢！";
                        returnContent = "";
                        errcode = (int)POIGlobalVar.errorCode.FAIL;

                        string alertVanillaMsg = jsonHandler.Serialize(new
                        {
                            resource = POIGlobalVar.resource.ALERTS,
                            alertType = POIGlobalVar.alertType.SYSTEM,
                            title = "版本更新",
                            message = "由于最新版本中问答系统进行了重大调整，一些旧版功能已不被支持，请及时更新版本，谢谢！",
                            timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now),
                        });

                        string pushVanillaMsg = jsonHandler.Serialize(new
                        {
                            resource = POIGlobalVar.resource.MESSAGES,
                            msgId = msgId,
                            userId = "",
                            sessionId = sessionId,
                            msgType = POIGlobalVar.messageType.SYSTEM,
                            message = "由于最新版本中问答系统进行了重大调整，一些旧版功能已不被支持，请及时更新版本，谢谢！",
                            timestamp = timestamp
                        });

                        if (userId != "")
                        {
                            List<string> pushList = new List<string>();
                            pushList.Add(userId);
                            POIProxyPushNotifier.send(pushList, alertVanillaMsg);
                            //POIProxyPushNotifier.send(pushList, pushVanillaMsg);
                        }

                        var responseVanillaErr = Request.CreateResponse(HttpStatusCode.OK);
                        responseVanillaErr.StatusCode = HttpStatusCode.ExpectationFailed;
                        responseVanillaErr.Content = new StringContent(jsonHandler.Serialize(new { status = returnStatus, type = type, errcode = errcode, errMsg = returnErrMsg, content = returnContent }));
                        return responseVanillaErr;

                    /*
                    case (int)POIGlobalVar.sessionType.JOIN:
                        if (POIProxySessionManager.checkUserBanList(userId))
                        {
                            returnStatus = (int)POIGlobalVar.errorCode.FAIL;
                            returnErrMsg = "您的账户被停权，无法提问，如有疑问请联系客服";
                            returnContent = "";
                            errcode = (int)POIGlobalVar.errorCode.FAIL;

                            string alertMsg = jsonHandler.Serialize(new
                            {
                                resource = POIGlobalVar.resource.ALERTS,
                                alertType = POIGlobalVar.alertType.SYSTEM,
                                //msgId = msgId,
                                title = "用户停权",
                                message = "您的账户被停权，无法提问，如有疑问请联系客服",
                                timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now),
                            });

                            if (userId != "")
                            {
                                List<string> pushList = new List<string>();
                                pushList.Add(userId);
                                POIProxyPushNotifier.send(pushList, alertMsg);
                            }

                            var responseErr = Request.CreateResponse(HttpStatusCode.OK);
                            responseErr.StatusCode = HttpStatusCode.ExpectationFailed;
                            responseErr.Content = new StringContent(jsonHandler.Serialize(new { status = returnStatus, type = type, errcode = errcode, errMsg = returnErrMsg, content = returnContent }));
                            return responseErr;
                        }

                        sessionId = msgInfo["sessionId"];
                        var sessionInfo = POIProxySessionManager.Instance.getSessionInfo(sessionId);

                        errcode = await wxJoinInteractiveSession(msgInfo, sessionInfo, pushMsg, userList);
                        if (errcode == (int)POIGlobalVar.errorCode.SUCCESS || errcode == (int)POIGlobalVar.errorCode.ALREADY_JOINED)
                        {
                            returnStatus = errcode;
                            returnErrMsg = "";
                            returnContent = jsonHandler.Serialize(POIProxySessionManager.Instance.getSessionArchive(sessionId));
                        }
                        else
                        {
                            returnStatus = errcode;
                            returnErrMsg = "";
                            returnContent = "";
                        }

                        break;

                    case (int)POIGlobalVar.sessionType.END:
                        sessionInfo = POIProxySessionManager.Instance.getSessionInfo(sessionId);
                        if (sessionInfo["creator"] == userId) 
                        {
                            errcode = (int)POIGlobalVar.errorCode.STUDENT_CANNOT_END;
                            break;
                        }
                        interMsgHandler.endInteractiveSession(msgId, userId, sessionId);
                        POIProxyPushNotifier.send(userList, pushMsg);
                        await POIProxyToWxApi.interactiveSessionEnded(sessionInfo["creator"], sessionId);

                        returnStatus = errcode;
                        returnErrMsg = "";
                        returnContent = "";

                        break;

                    case (int)POIGlobalVar.sessionType.CANCEL:
                        interMsgHandler.cancelInteractiveSession(msgInfo);
                        break;

                    case (int)POIGlobalVar.sessionType.RERAISE:
                        if (POIProxySessionManager.checkUserBanList(userId))
                        {
                            returnStatus = (int)POIGlobalVar.errorCode.FAIL;
                            returnErrMsg = "您的账户被停权，无法提问，如有疑问请联系客服";
                            returnContent = "";
                            errcode = (int)POIGlobalVar.errorCode.FAIL;

                            string alertMsg = jsonHandler.Serialize(new
                            {
                                resource = POIGlobalVar.resource.ALERTS,
                                alertType = POIGlobalVar.alertType.SYSTEM,
                                //msgId = msgId,
                                title = "用户停权",
                                message = "您的账户被停权，无法提问，如有疑问请联系客服",
                                timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now),
                            });

                            if (userId != "")
                            {
                                List<string> pushList = new List<string>();
                                pushList.Add(userId);
                                POIProxyPushNotifier.send(pushList, alertMsg);
                            }

                            var responseErr = Request.CreateResponse(HttpStatusCode.OK);
                            responseErr.StatusCode = HttpStatusCode.ExpectationFailed;
                            responseErr.Content = new StringContent(jsonHandler.Serialize(new { status = returnStatus, type = type, errcode = errcode, errMsg = returnErrMsg, content = returnContent }));
                            return responseErr;
                        }

                        if (POIProxySessionManager.Instance.checkDuplicatedCreatedSession(msgId))
                        {
                            sessionId = POIProxySessionManager.Instance.getSessionByMsgId(msgId);
                            returnStatus = (int)POIGlobalVar.errorCode.DUPLICATED;
                            returnErrMsg = "";
                            returnContent = jsonHandler.Serialize(new { sessionId = sessionId, timestamp = timestamp });
                            break;
                        }

                        string newSessionId = interMsgHandler.duplicateInteractiveSession(sessionId, timestamp);
                        interMsgHandler.reraiseInteractiveSession(msgId, userId, sessionId, newSessionId, timestamp);

                        POIProxyPushNotifier.send(userList, pushMsg);
                        if (WebConfigurationManager.AppSettings["ENV"] == "production")
                        {
                            broadCastCreateSession(sessionId, pushMsg);
                        }

                        await POIProxyToWxApi.interactiveSessionReraised(userId, sessionId, newSessionId);

                        returnContent = jsonHandler.Serialize(new { sessionId = newSessionId, timestamp = timestamp });

                        break;

                    case (int)POIGlobalVar.sessionType.RATING:
                        sessionInfo = POIProxySessionManager.Instance.getSessionInfo(sessionId);
                        string role = msgInfo.ContainsKey("role") ? msgInfo["role"] : "members";
                        if (!interMsgHandler.checkSessionState(sessionId, "closed") && !interMsgHandler.checkSessionState(sessionId, "cancelled"))
                        {
                            if (sessionInfo["creator"] != userId && role != "admin")
                            {
                                errcode = (int)POIGlobalVar.errorCode.TUTOR_CANNOT_RATING;
                                break;
                            }
                            rating = Convert.ToInt32(msgInfo["rating"]);
                            interMsgHandler.rateInteractiveSession(msgInfo);

                            Dictionary<string, string> pushDic = jsonHandler.Deserialize<Dictionary<string, string>>(pushMsg);
                            pushDic["rating"] = rating.ToString();
                            pushMsg = jsonHandler.Serialize(pushDic);
                            if (role == "members")
                            {
                                POIProxyPushNotifier.send(userList, pushMsg);
                            }
                            else
                            {
                                pushMsg = jsonHandler.Serialize(new
                                {
                                    resource = POIGlobalVar.resource.MESSAGES,
                                    msgId = msgId,
                                    userId = sessionInfo["creator"],
                                    sessionId = sessionId,
                                    msgType = (int)POIGlobalVar.messageType.SYSTEM,
                                    message = "由于对方长时间未评分，系统自动评为5分",
                                    timestamp = timestamp
                                });
                                userList.Remove(sessionInfo["creator"]);
                                POIProxyPushNotifier.send(userList, pushMsg);
                            }
                            //need to write for weixin tutor notifier.
                        }

                        break;
                    */

                    case (int)POIGlobalVar.sessionType.UPDATE:
                        desc = msgInfo.ContainsKey("description") ? msgInfo["description"] : "";
                        mediaId = msgInfo.ContainsKey("mediaId") ? msgInfo["mediaId"] : "";

                        Dictionary<string, string> infoDict = new Dictionary<string, string>();
                        infoDict["vote"] = msgInfo.ContainsKey("vote") ? msgInfo["vote"] : "0";
                        infoDict["watch"] = msgInfo.ContainsKey("watch") ? msgInfo["watch"] : "0";
                        infoDict["adopt"] = msgInfo.ContainsKey("adopt") ? msgInfo["adopt"] : "0";
                        string adoptScore = msgInfo.ContainsKey("adoptScore") ? msgInfo["adoptScore"] : "0";
                        POIProxySessionManager.Instance.updateSessionInfo(sessionId, infoDict, userId, adoptScore);

                        if (desc != "") interMsgHandler.updateQuestionDescription(sessionId, desc);
                        if (mediaId != "") interMsgHandler.updateQuestionMediaId(sessionId, mediaId);

                        break;

                    case (int)POIGlobalVar.sessionType.GET:
                        string sessionList = msgInfo.ContainsKey("sessionList") ? msgInfo["sessionList"] : "[]";
                        List<string> session = jsonHandler.Deserialize<List<string>>(sessionList);
                        var sessionDetail = POIProxySessionManager.Instance.getSessionDetail(session, userId);
                        returnContent = jsonHandler.Serialize(sessionDetail);

                        break;
                    
                    case (int)POIGlobalVar.sessionType.DELETE:
                        string deletedSessionList = msgInfo.ContainsKey("sessionList") ? msgInfo["sessionList"] : "[]";
                        List<string> deletedSession = jsonHandler.Deserialize<List<string>>(deletedSessionList);
                        foreach (var deletedItem in deletedSession) {
                            //List<Dictionary<string,string>> users = POIProxySessionManager.Instance.getUserListDetailsBySessionId(deletedItem);
                            if (interMsgHandler.checkSessionServing(deletedItem))
                            {
                                //foreach (var user in users)
                                //{
                                    //PPLog.debugLog("Deleted: " + DictToString(user, null));
                                    var deleteSessionInfo = POIProxySessionManager.Instance.getSessionInfo(deletedItem);
                                    if (deleteSessionInfo["creator"] == userId)
                                    {
                                        msgInfo["rating"] = "5";
                                        msgInfo["sessionId"] = "";
                                        interMsgHandler.rateInteractiveSession(msgInfo);
                                    }
                                    else
                                    {
                                        interMsgHandler.endInteractiveSession(msgId, userId, deletedItem);
                                    }
                                //}
                            }
                            POIProxySessionManager.Instance.deleteSession(deletedItem, userId);
                        }

                        break;

                    case (int)POIGlobalVar.sessionType.INVITE:
                        break;

                    case (int)POIGlobalVar.sessionType.SUBMIT_ANSWER:
                        string msgList = msgInfo.ContainsKey("answerList") ? msgInfo["answerList"] : "[]";
                        List<string> answerList = jsonHandler.Deserialize<List<string>>(msgList);
                        POIProxySessionManager.Instance.submitSessionAnswer(sessionId, answerList);

                        break;

                    case (int)POIGlobalVar.sessionType.RETRIEVE_ANSWER:
                        returnContent = jsonHandler.Serialize(POIProxySessionManager.Instance.retrieveSessionAnswer(sessionId)); 
                        break;

                    default:
                        break;
                }

                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = returnStatus, type = type, errcode = errcode, errMsg = returnErrMsg, content = returnContent }));
                return response;

            }
            catch (Exception e)
            {
                PPLog.errorLog("In wx to proxy post session: " + e.Message);
                if (e.Message.Contains("Unable to Connect") || e.Message.Contains("Redis Timeout expired"))
                {
                    POIProxyToWxApi.monitorLog(e.Message);
                }
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.ExpectationFailed;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.FAIL, content = e.Message }));
                return response;
            }
            
        }

        [HttpPost]
        public HttpResponseMessage Presentations(HttpRequestMessage request)
        {
            try
            {
                string content = request.Content.ReadAsStringAsync().Result;
                Dictionary<string, string> msgInfo = jsonHandler.Deserialize<Dictionary<string, string>>(content);
                PPLog.infoLog("[ProxyController presentations] " + DictToString(msgInfo, null));

                int type = int.Parse(msgInfo["type"]);
                string msgId = msgInfo["msgId"];
                string userId = msgInfo["userId"];
                string presId = msgInfo.ContainsKey("presId") ? msgInfo["presId"] : "";
                string messageList = msgInfo.ContainsKey("messageList") ? msgInfo["messageList"] : "";
                string sessionId = "";

                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                msgInfo["timestamp"] = timestamp.ToString();

                int returnStatus = 0;
                string returnErrMsg = "";
                string returnContent = "";
                int errcode = 0;

                switch (type)
                {
                    case (int)POIGlobalVar.presentationType.CREATE:
                        if (POIProxyPresentationManager.Instance.checkDuplicatedCreatedPresentation(msgId))
                        {
                            presId = POIProxyPresentationManager.Instance.getPresentationByMsgId(msgId);

                            returnStatus = (int)POIGlobalVar.errorCode.DUPLICATED;
                            returnErrMsg = "";
                            returnContent = jsonHandler.Serialize(new { presId = presId, timestamp = timestamp });

                            break;
                        }

                        string filter = msgInfo.ContainsKey("filter") ? msgInfo["filter"] : "";

                        presId = POIProxyPresentationManager.Instance.onPresentationCreate
                            (msgId, userId, msgInfo["mediaId"], msgInfo["description"], "private", filter);
                        
                        if (WebConfigurationManager.AppSettings["ENV"] == "production") { 
                            //broadCastCreateSession(presId, pushMsg);
                        }
                        
                        returnStatus = (int)POIGlobalVar.errorCode.SUCCESS;
                        returnErrMsg = "";
                        returnContent = jsonHandler.Serialize(new { presId = presId, timestamp = timestamp });

                        break;

                    case (int)POIGlobalVar.presentationType.JOIN:
                        if (POIProxySessionManager.Instance.checkDuplicatedCreatedSession(msgId))
                        {
                            sessionId = POIProxySessionManager.Instance.getSessionByMsgId(msgId);

                            returnStatus = (int)POIGlobalVar.errorCode.DUPLICATED;
                            returnErrMsg = "";
                            returnContent = jsonHandler.Serialize(new { sessionId = sessionId, timestamp = timestamp });

                            break;
                        }

                        if (POIProxyPresentationManager.Instance.checkDuplicatedPresentationJoin(presId, msgId))
                        {
                            sessionId = POIProxyPresentationManager.Instance.getPresentationSessionIdByUserId(presId, userId);

                            returnStatus = (int)POIGlobalVar.errorCode.DUPLICATED;
                            returnErrMsg = "";
                            returnContent = jsonHandler.Serialize(new { sessionId = sessionId, timestamp = timestamp });

                            break;
                        }

                        Tuple<string, List<string>> result = POIProxyPresentationManager.Instance.onPresentationJoin(msgId, userId, presId, timestamp, messageList);

                        if (result != null && result.Item1 != "-1")
                        {
                            returnStatus = (int)POIGlobalVar.errorCode.SUCCESS;
                            returnErrMsg = "";
                            returnContent = jsonHandler.Serialize(new { sessionId = result.Item1, msgList = result.Item2, timestamp = timestamp });
                        }
                        else
                        {
                            returnStatus = (int)POIGlobalVar.errorCode.FAIL;
                            returnErrMsg = "";
                            returnContent = "";
                        }
                        break;

                    case (int)POIGlobalVar.presentationType.END:
                        break;

                    case (int)POIGlobalVar.presentationType.PREPARE:
                        int prepareTime = int.Parse(msgInfo["prepareTime"]);
                        double targetTime = POIProxyPresentationManager.Instance.onPresentationPrepare(msgId, presId, userId, timestamp, prepareTime);

                        returnStatus = (int)POIGlobalVar.errorCode.SUCCESS;
                        returnErrMsg = "";
                        returnContent = jsonHandler.Serialize(new { targetTime = targetTime });
                        break;

                    case (int)POIGlobalVar.presentationType.UPDATE:
                        Dictionary<string, string> infoDict = new Dictionary<string, string>();
                        if (msgInfo.ContainsKey("description"))
                            infoDict["description"] = msgInfo["description"];
                        if (msgInfo.ContainsKey("mediaId"))
                            infoDict["cover"] = msgInfo["mediaId"];
                        infoDict["difficulty"] = msgInfo.ContainsKey("difficulty") ? msgInfo["difficulty"] : "0";
                        POIProxyPresentationManager.Instance.onPresentationUpdate(presId, infoDict, userId);
                        break;

                    case (int)POIGlobalVar.presentationType.GET:
                        string presGetListStr = msgInfo.ContainsKey("presList") ? msgInfo["presList"] : "[]";
                        bool prepareFlag = msgInfo.ContainsKey("prepareInfo");
                        double lastTimestamp = (prepareFlag) ? double.Parse(msgInfo["prepareInfo"]) : 0;
                        List<string> presGetList = jsonHandler.Deserialize<List<string>>(presGetListStr);
                        var presGetDetail = POIProxyPresentationManager.Instance.onPresentationGet(presGetList, userId, prepareFlag, lastTimestamp);
                        returnContent = jsonHandler.Serialize(presGetDetail);
                        break;
                    
                    case (int)POIGlobalVar.presentationType.QUERY:
                        string presQueryListStr = msgInfo.ContainsKey("presList") ? msgInfo["presList"] : "[]";
                        List<string> presQueryList = jsonHandler.Deserialize<List<string>>(presQueryListStr);
                        var presQueryDetail = POIProxyPresentationManager.Instance.onPresentationQuery(presQueryList);
                        returnContent = jsonHandler.Serialize(presQueryDetail);
                        break;

                    case (int)POIGlobalVar.presentationType.DELETE:
                        POIProxyPresentationManager.Instance.onPresentationDelete(presId, userId);
                        break;

                    default:
                        break;

                }

                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = returnStatus, type = type, errMsg = returnErrMsg, content = returnContent }));
                return response;
            }
            catch (Exception e)
            {
                PPLog.errorLog("In wx to proxy post presentation: " + e.Message);
                if (e.Message.Contains("Unable to Connect") || e.Message.Contains("Redis Timeout expired"))
                {
                    POIProxyToWxApi.monitorLog(e.Message);
                }
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.ExpectationFailed;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.FAIL, content = e.Message }));
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
            string deviceId = userInfo.ContainsKey("deviceId") ? userInfo["deviceId"] : "";
            string userId = userInfo.ContainsKey("userId") ? userInfo["userId"] : "";
            string system = userInfo.ContainsKey("system") ? userInfo["system"] : "";
            int tag = userInfo.ContainsKey("tag") ? int.Parse(userInfo["tag"]) : 0;

            try
            {
                switch (type)
                {
                    case (int)POIGlobalVar.userType.UPDATE:
                        
                        if (deviceId != "" && userId != "" && system != "")
                        {
                            POIProxySessionManager.Instance.updateUserDevice(userId, deviceId, system, tag);
                        }
                        else
                        {
                            POIProxySessionManager.Instance.updateUserInfoFromDb(userId);
                        }
                        break;

                    case (int)POIGlobalVar.userType.SCORE:
                        //upload tutorial without interactive session.
                        interMsgHandler.addSessionScore(userId, "tutorial");
                        break;

                    case (int)POIGlobalVar.userType.LOGOUT:
                        if (deviceId != "" && userId != "" && system != "")
                        {
                            POIProxySessionManager.Instance.updateUserDevice(userId, deviceId, system, tag);
                        }
                        break;

                    default:
                        break;
                }
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.SUCCESS }));
                return response;
            }
            catch (Exception e)
            {
                PPLog.errorLog("error in users operation received: " + e.Message);
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.ExpectationFailed;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.FAIL, content = e.Message }));
                return response;
            }
        }

        public HttpResponseMessage Services(HttpRequestMessage request)
        {
            try
            {
                string content = request.Content.ReadAsStringAsync().Result;
                Dictionary<string, string> serviceInfo = jsonHandler.Deserialize<Dictionary<string, string>>(content);
                PPLog.infoLog("[ProxyController Services] " + DictToString(serviceInfo, null));

                string serviceType = serviceInfo["serviceType"];
                string msgId = serviceInfo["msgId"];
                string title = serviceInfo.ContainsKey("title") ? serviceInfo["title"] : "";
                string mediaId = serviceInfo.ContainsKey("mediaId") ? serviceInfo["mediaId"] : "";
                string url = serviceInfo.ContainsKey("url") ? serviceInfo["url"] : "";
                string message = serviceInfo.ContainsKey("message") ? serviceInfo["message"] : "";
                string userId = serviceInfo.ContainsKey("userId") ? serviceInfo["userId"] : "";
                string taskScore = serviceInfo.ContainsKey("taskScore") ? serviceInfo["taskScore"] : "0";
                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                string pushMsg = jsonHandler.Serialize(new
                {
                    resource = POIGlobalVar.resource.SERVICES,
                    serviceType = serviceType,
                    msgId = msgId,
                    title = title,
                    message = message,
                    mediaId = mediaId,
                    url = url,
                    timestamp = timestamp,
                });

                if (int.Parse(serviceType) == (int)POIGlobalVar.serviceType.TASK)
                {
                    Dictionary<string, object> pushDic = jsonHandler.Deserialize<Dictionary<string, object>>(pushMsg);
                    pushDic["extra"] = jsonHandler.Serialize(new { taskScore = taskScore });
                    pushMsg = jsonHandler.Serialize(pushDic);
                }

                if (userId != "")
                {
                    List<string> userList = new List<string>();
                    userList.Add(userId);
                    POIProxyPushNotifier.send(userList, pushMsg);
                }
                else 
                {
                    if (WebConfigurationManager.AppSettings["ENV"] == "production") { 
                        POIProxyPushNotifier.broadcast(pushMsg, title);
                    }
                }

                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.SUCCESS, type = POIGlobalVar.resource.SERVICES }));
                return response;
            }
            catch (Exception e)
            {
                PPLog.errorLog("error in service received: " + e.Message);
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.ExpectationFailed;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.FAIL, content = e.Message }));
                return response;
            }
        }

        public HttpResponseMessage Alerts(HttpRequestMessage request)
        {
            try
            {
                string content = request.Content.ReadAsStringAsync().Result;
                Dictionary<string, string> alertInfo = jsonHandler.Deserialize<Dictionary<string, string>>(content);
                PPLog.infoLog("[ProxyController Alerts] " + DictToString(alertInfo, null));

                string alertType = alertInfo["alertType"];
                string msgId = alertInfo["msgId"];
                string title = alertInfo.ContainsKey("title") ? alertInfo["title"] : "";
                string message = alertInfo.ContainsKey("message") ? alertInfo["message"] : "";
                string userId = alertInfo.ContainsKey("userId") ? alertInfo["userId"] : "";
                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                string pushMsg = jsonHandler.Serialize(new
                {
                    resource = POIGlobalVar.resource.ALERTS,
                    alertType = alertType,
                    msgId = msgId,
                    title = title,
                    message = message,
                    timestamp = timestamp,
                });

                if (userId != "")
                {
                    List<string> userList = new List<string>();
                    userList.Add(userId);
                    POIProxyPushNotifier.send(userList, pushMsg);
                }
                else
                {
                    if (WebConfigurationManager.AppSettings["ENV"] == "production") { 
                        POIProxyPushNotifier.broadcast(pushMsg, title);
                    }
                }

                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.SUCCESS, type = POIGlobalVar.resource.ALERTS }));
                return response;
            }
            catch (Exception e)
            {
                PPLog.errorLog("error in alert received: " + e.Message);
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.ExpectationFailed;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.FAIL, content = e.Message }));
                return response;
            }
        }

        public HttpResponseMessage Sync(HttpRequestMessage request)
        {
            try
            {
                string content = request.Content.ReadAsStringAsync().Result;
                Dictionary<string, object> syncInfo = jsonHandler.Deserialize<Dictionary<string, object>>(content);
                //PPLog.infoLog("[ProxyController Sync] " + DictToString(syncInfo, null));

                int type = syncInfo.ContainsKey("type") ? int.Parse((string)syncInfo["type"]) : 0;

                string hash = syncInfo.ContainsKey("hash") ? (string)syncInfo["hash"] : "";
                string hashPres = syncInfo.ContainsKey("hashPres") ? (string)syncInfo["hashPres"] : "";
                string userId = syncInfo.ContainsKey("userId") ? (string)syncInfo["userId"] : "";

                int syncStatus = (int)POIGlobalVar.errorCode.SESSION_SYNC;
                string syncContent = "";

                switch (type)
                {
                    case (int)POIGlobalVar.syncType.SESSION:
                        ArrayList sessionList = syncInfo.ContainsKey("sessionList") ? (ArrayList) syncInfo["sessionList"] : new ArrayList();

                        if (userId != "")
                        {
                            if (hash != "")
                            {
                                bool isSync = POIProxySessionManager.Instance.checkSyncReference(userId, hash);
                                if (isSync)
                                {
                                    syncStatus = (int)POIGlobalVar.errorCode.SESSION_SYNC;
                                }
                                else
                                {
                                    var session_by_user = POIProxySessionManager.Instance.getSessionsByUserId(userId);
                                    var serverSessionList = new List<string>();
                                    foreach (var session in session_by_user)
                                    {
                                        if (session.Value != "0" && session.Value != "-1")
                                        {
                                            serverSessionList.Add(session.Key);
                                        }
                                    }

                                    syncStatus = (int)POIGlobalVar.errorCode.SESSION_ASYNC;
                                    syncContent = jsonHandler.Serialize(serverSessionList);
                                }
                            }
    
                            else if (sessionList.Count > 0)
                            {
                                var session_by_user = POIProxySessionManager.Instance.getSessionsByUserId(userId);
    
                                List<object> missedSessionList = new List<object>();
                                foreach (var session in session_by_user)
                                {
                                    double client_timestamp = 0;
                                    foreach (Dictionary<string, object> sessionRecord in sessionList)
                                    {
                                        if (sessionRecord.ContainsKey(session.Key))
                                        {
                                            if (double.Parse((string)sessionRecord[session.Key]) == double.Parse(session.Value))
                                            {
                                                client_timestamp = double.Parse(session.Value);
                                                break;
                                            }
                                        }
                                    }
                                    if (session.Value != "0" && session.Value != "-1")
                                    {
                                        var missedEvents = interMsgHandler.getMissedEventsInSession(session.Key, client_timestamp);
                                        Dictionary<string, object> missedDic = new Dictionary<string, object>();
                                        missedDic[session.Key] = missedEvents;
                                        missedSessionList.Add(missedDic);
                                    }
                                }
    
                                if (missedSessionList.Count > 0)
                                {
                                    syncStatus = (int)POIGlobalVar.errorCode.SESSION_ASYNC;
                                    syncContent = jsonHandler.Serialize(missedSessionList);
                                    //PPLog.debugLog("[POIProxySessionManager] checkSyncReference missed session: " + syncContent);
                                }
                                else
                                {
                                    syncStatus = (int)POIGlobalVar.errorCode.SESSION_ASYNC;
                                }
                            }   
                        }
                        else
                        { 
                        }

                        break;

                    case (int)POIGlobalVar.syncType.PRESENTATION:
                        if (hash != "" && hashPres != "" && userId != "")
                        {
                            if (POIProxySessionManager.Instance.checkSyncReference(userId, hash) &&
                                POIProxyPresentationManager.Instance.checkPresSync(userId, hashPres))
                            {
                                syncStatus = (int)POIGlobalVar.errorCode.SESSION_SYNC;
                            }
                            else
                            {
                                var session_by_user = POIProxySessionManager.Instance.getSessionsByUserId(userId);
                                var serverSessionList = new List<string>();
                                foreach (var session in session_by_user)
                                {
                                    if (session.Value != "0" && session.Value != "-1")
                                    {
                                        serverSessionList.Add(session.Key);
                                    }
                                }

                                var serverPresList = POIProxyPresentationManager.Instance.getUserPresentationList(userId);

                                string sessionInfo = jsonHandler.Serialize(serverSessionList);
                                string presInfo = jsonHandler.Serialize(serverPresList);
                                syncStatus = (int)POIGlobalVar.errorCode.SESSION_ASYNC;
                                syncContent = jsonHandler.Serialize(new { sessionList = sessionInfo, presList = presInfo});
                            }
                        }
                        break;

                    default:
                        break;
                }

                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = syncStatus, content = syncContent }));
                return response;

            }
            catch (Exception e)
            {
                PPLog.errorLog("error in sync received: " + e.Message);
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.StatusCode = HttpStatusCode.ExpectationFailed;
                response.Content = new StringContent(jsonHandler.Serialize(new { status = POIGlobalVar.errorCode.FAIL, content = e.Message }));
                return response;
            }
        }

        private async Task<int> wxJoinInteractiveSession(Dictionary<string, string> msgInfo, Dictionary<string, string> sessionInfo, string pushMsg, List<string> userList)
        {
            string msgId = msgInfo["msgId"];
            string sessionId = msgInfo["sessionId"];
            string userId = msgInfo["userId"];
            var userInfo = POIProxySessionManager.Instance.getUserInfo(userId);
            string userInfoJson = jsonHandler.Serialize(userInfo);

            if (double.Parse(sessionInfo["create_at"])
                >= POITimestamp.ConvertToUnixTimestamp(DateTime.Now.AddSeconds(-60)) && WebConfigurationManager.AppSettings["ENV"] == "production")
            {
                PPLog.infoLog("Cannot join the session, not passing time limit");
                //Notify the weixin user about the join failed
                await POIProxyToWxApi.interactiveSessionJoinBeforeTimeLimit(userId, sessionId);
                return (int)POIGlobalVar.errorCode.TIME_LIMITED;
            }
            else if (!interMsgHandler.checkSessionOpen(sessionId))
            {
                PPLog.infoLog("[POIProxyHub wxJoinInteractiveSession] session status is not open");
                return (int)POIGlobalVar.errorCode.SESSION_NOT_OPEN;
            }
            else if (POIProxySessionManager.Instance.checkUserInSession(sessionId, userId))
            {
                //User already in the session
                PPLog.infoLog("[POIProxyHub wxJoinInteractiveSession] Session already joined");

                //Notify the weixin users about the join operation
                await POIProxyToWxApi.interactiveSessionJoined(userId, sessionId);
                return (int)POIGlobalVar.errorCode.ALREADY_JOINED;
            }
            else if (userInfo["accessRight"] != "tutor") {
                PPLog.infoLog("[POIProxyHub wxJoinInteractiveSession] forbidden join");
                return (int)POIGlobalVar.errorCode.STUDENT_CANNOT_JOIN;
            }
            else if (POIProxySessionManager.Instance.acquireSessionToken(sessionId))
            {
                PPLog.infoLog("[POIProxyHub wxJoinInteractiveSession] Session is open, joined!");
                double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                interMsgHandler.joinInteractiveSession(msgId, userId, sessionId, timestamp);

                //Send push notification
                POIProxyPushNotifier.send(userList, pushMsg);

                //Notify the weixin users about the join operation
                await POIProxyToWxApi.interactiveSessionJoined(userId, sessionId);

                //Notify the wexin server about the join operation
                await POIProxyToWxApi.interactiveSessionNewUserJoined(sessionInfo["creator"], sessionId, userInfoJson);

                return 0;
            }
            else
            {
                PPLog.infoLog("[POIProxyHub wxJoinInteractiveSession] Cannot join the session, taken by others");
                //Notify the weixin user about the join failed
                await POIProxyToWxApi.interactiveSessionJoinFailed(userId, sessionId);
                return (int)POIGlobalVar.errorCode.TAKEN_BY_OTHERS;
            }
        }

        private void broadCastCreateSession(string sessionId, string pushMsg)
        {
            Dictionary<string, object> tempDic = jsonHandler.Deserialize<Dictionary<string, object>>(pushMsg);
            tempDic["sessionType"] = POIGlobalVar.sessionType.CREATE;
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