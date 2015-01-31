using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using Microsoft.AspNet.SignalR;
using System.Data;
using System.Web.Script.Serialization;
using System.Web.Configuration;

using System.Threading.Tasks;

namespace POIProxy
{
    public class POIProxyInteractiveMsgHandler 
    {
        IHubContext hubContext = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
        POIProxyDbManager dbManager = POIProxyDbManager.Instance;
        JavaScriptSerializer jsonHandler = new JavaScriptSerializer();

        
        public void addUserToSessionRecord(string userId, string sessionId)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["user_id"] = userId;
            values["type"] = "session";
            values["content_id"] = sessionId;
            values["user_right"] = 4; //4 represents connected using signalr connection
            values["created_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            dbManager.insertIntoTable("user_right", values);
        }

        public void addQuestionActivity(string userId, string sessionId)
        {
            var studentInfo = POIProxySessionManager.Instance.getUserInfo(userId);
            var sessionInfo = POIProxySessionManager.Instance.getSessionInfo(sessionId);

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["user_id"] = userId;
            values["type"] = "int_session_question";
            values["content_id"] = sessionId;
            values["create_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            values["data"] = jsonHandler.Serialize(
                new Dictionary<string, object>
                {
                    {"session", sessionInfo},
                    {"student", studentInfo},
                }
            );

            dbManager.insertIntoTable("activity", values);
        }

        public void addAnswerActivity(string userId, string sessionId)
        {
            var sessionInfo = POIProxySessionManager.Instance.getSessionInfo(sessionId);
            var studentInfo = POIProxySessionManager.Instance.getUserInfo(sessionInfo["creator"]);
            var tutorInfo = POIProxySessionManager.Instance.getUserInfo(userId);

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["user_id"] = userId;
            values["type"] = "int_session_answer";
            values["content_id"] = sessionId;
            values["data"] = jsonHandler.Serialize(
                new Dictionary<string, object>
                {
                    {"session", sessionInfo},
                    {"student", studentInfo},
                    {"tutor", tutorInfo}
                }
            );
            
            values["create_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            dbManager.insertIntoTable("activity", values);
        }

        public void updateAnswerActivity(string userId, string sessionId, int rating)
        {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["content_id"] = sessionId;
            conditions["type"] = "int_session_answer";

            var sessionInfo = POIProxySessionManager.Instance.getSessionInfo(sessionId);
            var studentInfo = POIProxySessionManager.Instance.getUserInfo(sessionInfo["creator"]);
            var tutorInfo = POIProxySessionManager.Instance.getUserInfo(userId);

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["data"] = jsonHandler.Serialize(
                new Dictionary<string, object>
                {
                    {"session", sessionInfo},
                    {"student", studentInfo},
                    {"tutor", tutorInfo}
                }
            );

            dbManager.updateTable("activity", values, conditions);
        }

        public void updateSessionStatus(string sessionId, string status)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["status"] = status;

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;

            dbManager.updateTable("session", values, conditions);
        }

        public void updateSessionStatusWithTutorJoin(string userId, string sessionId)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["status"] = "serving";
            values["start_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            values["tutor"] = userId;

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;

            dbManager.updateTable("session", values, conditions);
        }

        public void updateSessionStatusWithRating(string sessionId, int rating, bool updateEndTime)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["status"] = "closed";
            values["rating"] = rating;
            values["rate_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;

            //Check if end_at attribute needs to be ended
            if (updateEndTime)
            {
                values["end_at"] = values["rate_at"];
                conditions["status"] = "serving";
            }
            else
            {
                conditions["status"] = "session_end_waiting";
            }

            dbManager.updateTable("session", values, conditions);
        }

        public void updateSessionStatusWithEnding(string sessionId)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["status"] = "session_end_waiting";
            values["end_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;

            dbManager.updateTable("session", values, conditions);
        }

        public bool checkSessionOpen(string sessionId)
        {
            return checkSessionState(sessionId, "open");
        }

        public bool checkSessionServing(string sessionId)
        {
            return checkSessionState(sessionId, "serving");
        }

        public bool checkSessionState(string sessionId, string state)
        {
            bool isInState = false;

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;
            conditions["status"] = state;

            List<string> cols = new List<string>();
            cols.Add("id");

            DataTable result = dbManager.selectFromTable("session", cols, conditions);
            if (result.Rows.Count > 0)
            {
                PPLog.infoLog("[POIProxyInteractiveMsgHandler checkSessionState] found session open " + result.Rows[0]["id"]);
                isInState = true;
            }

            return isInState;
        }

        public DataRow getSessionState(string sessionId)
        {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;

            return dbManager.selectSingleRowFromTable("session", null, conditions);
        }

        public void addSessionScore(string userId, string type) {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["tutor"] = userId;

            List<string> cols = new List<string>();
            cols.Add("start_at");
            cols.Add("type");

            DataTable result = dbManager.selectFromTable("session", cols, conditions);
            if (result.Rows.Count > 0)
            {
                bool isPersistent = false;

                int interactiveSessionNum = 0;
                int tutorialSessionNum = 0;

                int TUTORIALRULE = 2;
                int INTERACTIVERULE = 2;

                if (WebConfigurationManager.AppSettings["ENV"] == "production") {
                    TUTORIALRULE = 10;
                    INTERACTIVERULE = 10;
                }

                double today = POITimestamp.ConvertToUnixTimestamp(DateTime.Today);
                double yesterday =  today - 24 * 60 * 60;
                for (int i = 0; i < result.Rows.Count; i++)
                {
                    if ((double)result.Rows[i]["start_at"] > today) {
                        if ((string)result.Rows[i]["type"] == "interactive")
                        {
                            interactiveSessionNum++;
                        }
                        else if ((string)result.Rows[i]["type"] == "public")
                        { 
                            tutorialSessionNum++;
                        }
                    }
                    if ((double)result.Rows[i]["start_at"] > yesterday && (double)result.Rows[i]["start_at"] < today)
                    {
                        isPersistent = true;
                    }
                }
                PPLog.debugLog("interactiveSessionNum: " + interactiveSessionNum + " tutorialSessionNum: " + tutorialSessionNum + " isPersistent: " + isPersistent);

                Dictionary<string, object> userConditions = new Dictionary<string, object>();
                userConditions["user_id"] = userId;

                List<string> userCols = new List<string>();
                userCols.Add("persistent_time");
                userCols.Add("persistent_score");
                userCols.Add("interactive_score_reward");
                userCols.Add("tutorial_score_reward");
                userCols.Add("tutorial_score");

                DataRow userResult = getByUserId(userConditions, userCols, "user_score");
                int persistentTime = (int)userResult["persistent_time"];
                int persistentScore = (int)userResult["persistent_score"];
                int interactiveScoreReward = (int)userResult["interactive_score_reward"];
                int tutorialScoreReward = (int)userResult["tutorial_score_reward"];
                int tutorialScore = (int)userResult["tutorial_score"];

                List<string> userList = new List<string>();
                userList.Add(userId);

                string pushMsg = jsonHandler.Serialize(new
                {
                    resource = POIGlobalVar.resource.SERVICES,
                    serviceType = (int)POIGlobalVar.serviceType.TASK,
                    msgId = POITimestamp.ConvertToUnixTimestamp(DateTime.Now).ToString(),
                    mediaId = "",
                    url = "",
                    timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now)
                });

                if (isPersistent)
                {
                    if ((interactiveSessionNum + tutorialSessionNum) == 1)
                    {
                        updateByUserId(userId, "persistent_time", persistentTime + 1, "user_score");
                        persistentTime = persistentTime > 5 ? 5 : persistentTime;
                        updateByUserId(userId, "persistent_score", persistentScore + 10 * persistentTime, "user_score");
                        POIProxySessionManager.Instance.updateUserScoreRanking(userId, persistentTime * 10);
                        insertUserTask(userId, "1");

                        if (persistentTime > 0) {
                            Dictionary<string, object> pushDic = jsonHandler.Deserialize<Dictionary<string, object>>(pushMsg);
                            pushDic["title"] = "完成任务-我来守护者";
                            pushDic["message"] = "持续活跃" + userResult["persistent_time"].ToString() + "天奖励" + (10 * persistentTime).ToString() + "分";
                            pushDic["extra"] = jsonHandler.Serialize(new { taskScore = (10 * persistentTime).ToString() });
                            pushMsg = jsonHandler.Serialize(pushDic);
                            POIProxyPushNotifier.send(userList, pushMsg);
                        }
                    }
                }
                else
                {
                    updateByUserId(userId, "persistent_time", 1, "user_score");
                }

                if (interactiveSessionNum == TUTORIALRULE && type == "interactive")
                {
                    updateByUserId(userId, "interactive_score_reward", interactiveScoreReward + 100, "user_score");
                    POIProxySessionManager.Instance.updateUserScoreRanking(userId, 100);
                    insertUserTask(userId, "2");

                    Dictionary<string, object> pushDic = jsonHandler.Deserialize<Dictionary<string, object>>(pushMsg);
                    pushDic["title"] = "完成任务-答疑超人";
                    pushDic["message"] = "今日完成十个答疑奖励100分";
                    pushDic["extra"] = jsonHandler.Serialize(new { taskScore = (100).ToString() });
                    pushMsg = jsonHandler.Serialize(pushDic);
                    POIProxyPushNotifier.send(userList, pushMsg);
                }

                if (tutorialSessionNum == INTERACTIVERULE && type == "tutorial")
                {
                    updateByUserId(userId, "tutorial_score_reward", tutorialScoreReward + 100, "user_score");
                    POIProxySessionManager.Instance.updateUserScoreRanking(userId, 100);
                    insertUserTask(userId, "3");

                    Dictionary<string, object> pushDic = jsonHandler.Deserialize<Dictionary<string, object>>(pushMsg);
                    pushDic["title"] = "完成任务-小课堂名师";
                    pushDic["message"] = "今日完成十个小课堂奖励100分";
                    pushDic["extra"] = jsonHandler.Serialize(new { taskScore = (100).ToString() });
                    pushMsg = jsonHandler.Serialize(pushDic);
                    POIProxyPushNotifier.send(userList, pushMsg);
                }

                if (type == "tutorial")
                {
                    updateByUserId(userId, "tutorial_score", tutorialScore + 10, "user_score");
                    POIProxySessionManager.Instance.updateUserScoreRanking(userId, 10);
                }

            }
        }

        private void updateByUserId(string userId, string colName, int value, string table){
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["user_id"] = userId;
            values[colName] = value;

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["user_id"] = userId;

            dbManager.updateTable(table, values, conditions);
        }

        private void insertUserTask(string userId, string taskId) {
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["updated_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now).ToString();
            values["user_id"] = userId;
            values["task_id"] = taskId;

            dbManager.insertIntoTable("user_task", values);
        }

        private DataRow getByUserId(Dictionary<string, object> conditions, List<string> cols, string table)
        {
            DataTable userResult = dbManager.selectFromTable("user_score", cols, conditions);
            if (userResult.Rows.Count > 0)
            {
                return userResult.Rows[0];
            }
            else 
            {
                Dictionary<string, object> values = new Dictionary<string, object>();
                values["user_id"] = conditions["user_id"];
                string userScoreId = dbManager.insertIntoTable("user_score", values);
                return dbManager.selectFromTable(table, cols, conditions).Rows[0];
            }
        }

        public Tuple<string,string> createInteractiveSession(string msgId, string userId, string mediaId, 
            string desc, string accessType = "private", string filter = "")
        {
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            //Create interactive presentation
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["user_id"] = userId;
            values["type"] = "interactive";
            values["course_id"] = -1;
            values["description"] = desc;
            values["create_at"] = timestamp;
            values["media_id"] = mediaId;

            string presId = dbManager.insertIntoTable("presentation", values);

            values.Clear();
            values["type"] = "interactive";
            values["presId"] = (presId == "-1") ? "0" : presId;
            values["creator"] = userId;
            values["create_at"] = timestamp;
            values["status"] = "open";

            string sessionId = dbManager.insertIntoTable("session", values);

            Dictionary<string, string> filterInfo = jsonHandler.Deserialize<Dictionary<string, string>>(filter);
            values.Clear();
            values["pid"] = presId;
            if (filter != "")
            {
                values["gid"] = filterInfo.ContainsKey("gid") ? filterInfo["gid"] : null;
                values["sid"] = filterInfo.ContainsKey("sid") ? filterInfo["sid"] : null;
                values["cid"] = filterInfo.ContainsKey("cid") ? filterInfo["cid"] : null;
            }
            else
            {
                values["gid"] = null;
                values["sid"] = null;
                values["cid"] = null;
            }
            dbManager.insertIntoTable("pres_category", values);
            

            //Insert record into the database for the user to session relationship
            //addUserToSessionRecord(userId, sessionId);

            //Get the information about the activity
            var userInfo = POIProxySessionManager.Instance.getUserInfo(userId);
            Dictionary<string, string> infoDict = new Dictionary<string, string>();
            infoDict["session_id"] = sessionId;
            infoDict["pres_id"] = presId;
            infoDict["create_at"] =  timestamp.ToString();
            infoDict["creator"] = userId;
            infoDict["description"] = desc;
            infoDict["cover"] = mediaId;
            infoDict["access_type"] = accessType;
            infoDict["user_id"] = userInfo["user_id"];
            infoDict["username"] = userInfo["username"];
            infoDict["avatar"] = userInfo["avatar"];

            try {
                POIProxySessionManager.Instance.updateSessionInfo(sessionId, infoDict);
            }
            catch (Exception e)
            {
                PPLog.errorLog("redis error:" + e.Message);
            }
            
            //Update redis presentation info
            infoDict.Clear();
            infoDict["pres_id"] = presId;
            infoDict["create_at"] = timestamp.ToString();
            infoDict["creator"] = userId;
            infoDict["description"] = desc;
            infoDict["cover"] = mediaId;
            infoDict["cid"] = filterInfo.ContainsKey("cid") ? filterInfo["cid"] : "0";
            infoDict["gid"] = filterInfo.ContainsKey("gid") ? filterInfo["gid"] : "0";
            infoDict["sid"] = filterInfo.ContainsKey("sid") ? filterInfo["sid"] : "0";
            infoDict["access_type"] = accessType;
            infoDict["user_id"] = userInfo["user_id"];
            infoDict["username"] = userInfo["username"];
            infoDict["realname"] = userInfo["realname"];
            infoDict["avatar"] = userInfo["avatar"];
            infoDict["vanilla"] = "1";
            infoDict["interactive"] = sessionId;
            
            try
            {
                POIProxyPresentationManager.Instance.onPresentationUpdate(presId, infoDict);
            }
            catch (Exception e)
            {
                PPLog.errorLog("redis error:" + e.Message);
            }

            //Archive the session created event
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "session_created",
                EventId = msgId.ToString(),
                MediaId = "",
                UserId = userId,
                Timestamp = timestamp,
                Message = "",
                Data = infoDict
            };
            //Subscribe the user to the session
            POIProxySessionManager.Instance.subscribeSession(sessionId, userId);
            POIProxyPresentationManager.Instance.updateUserPresentation(userId, presId, (int)POIGlobalVar.presentationType.CREATE);

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEvent);
            POIProxySessionManager.Instance.createSessionEvent(sessionId, poiEvent);

            //Insert the question activity into the activity table
            addQuestionActivity(userId, sessionId);

            PPLog.debugLog("[POIProxyInteractiveMsgHandler createInteractiveSession] session created! session id: "+ sessionId);
            return new Tuple<string,string>(presId, sessionId);
        }

        public void joinInteractiveSession(string msgId, string userId, string sessionId, double timestamp)
        {
            //add the current user into the session table
            addUserToSessionRecord(userId, sessionId);
            

            //Turn the session to serving status
            updateSessionStatusWithTutorJoin(userId, sessionId);

            //Archive the session join information
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                EventType = "session_joined",
                EventId = msgId,
                MediaId = "",
                UserId = userId,
                Timestamp = timestamp,
                Message = "",
                Data = POIProxySessionManager.Instance.getUserInfo(userId)
            };

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEvent);

            //Subscribe the user to the session
            POIProxySessionManager.Instance.subscribeSession(sessionId, userId);

            //Add the activity record
            addAnswerActivity(userId, sessionId);

            //add user score.
            addSessionScore(userId, "interactive");
        }

        public void cancelInteractiveSession(Dictionary<string,string>msgInfo)
        {
            string msgId = msgInfo["msgId"];
            string userId = msgInfo["userId"];
            string sessionId = msgInfo["sessionId"];
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            //Set the status to cancelled
            updateSessionStatus(sessionId, "cancelled");

            //Upload the session archive to the qiniu cdn
            string mediaId = POICdnHelper.generateCdnKeyForSessionArchive(sessionId);
            POICdnHelper.uploadStrToQiniuCDN(mediaId, jsonHandler.Serialize(POIProxySessionManager.Instance.getSessionArchive(sessionId)));

            //Update the database given the media id
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["media_id"] = mediaId;

            dbManager.updateTable("session", values, conditions);

            //Unsubscribe the old session
            POIProxySessionManager.Instance.unsubscribeSession(sessionId, userId);

            //Archive the cancel event
            POIInteractiveEvent cancelEvent = new POIInteractiveEvent
            {
                EventType = "session_cancelled",
                EventId = msgId,
                UserId = userId,
                Timestamp = timestamp,
            };

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, cancelEvent);
        }

        public void reraiseInteractiveSession(string msgId, string userId, string sessionId, string newSessionId, double timestamp)
        {
            //Set the status to cancelled for the initial session
            updateSessionStatus(sessionId, "cancelled");

            //Unsubscribe the old session
            POIProxySessionManager.Instance.unsubscribeSession(sessionId, userId);

            //Archive the cancel event
            POIInteractiveEvent cancelEvent = new POIInteractiveEvent
            {
                EventType = "session_cancelled",
                EventId = msgId,
                UserId = userId,
                Timestamp = timestamp,
            };

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, cancelEvent);

            Dictionary<string, string> info = POIProxySessionManager.Instance.getSessionInfo(sessionId);

            //Upload the session archive
            string mediaId = POICdnHelper.generateCdnKeyForSessionArchive(sessionId);
            POICdnHelper.uploadStrToQiniuCDN(mediaId, jsonHandler.Serialize(POIProxySessionManager.Instance.getSessionArchive(sessionId)));

            //Update the database given the media id
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["media_id"] = mediaId;

            dbManager.updateTable("session", values, conditions);

            //Update the new session info
            POIProxySessionManager.Instance.updateSessionInfo(sessionId,
                new Dictionary<string, string>
                {
                    {"session_id", newSessionId},
                    {"create_at", timestamp.ToString()},
                    {"creator", userId},
                    {"cover", info["cover"]},
                    {"description", info["description"]}
                }
            );

            //Subscribe the user to the session
            POIProxySessionManager.Instance.subscribeSession(newSessionId, userId);

            //Archive the create event
            POIInteractiveEvent createEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "session_created",
                EventId = msgId,
                MediaId = "",
                UserId = userId,
                Timestamp = timestamp,
                Message = "",
                Data = POIProxySessionManager.Instance.getSessionInfo(newSessionId)
            };

            POIProxySessionManager.Instance.archiveSessionEvent(newSessionId, createEvent);
            POIProxySessionManager.Instance.createSessionEvent(newSessionId, createEvent);
        }

        public bool checkAndProcessArchiveDuringSessionEnd(string sessionId)
        {
            PPLog.infoLog("[POIProxyInteractiveMsgHandler checkAndProcessArchiveDuringSessionEnd] Session id is " + sessionId);

            //Check if the session is in the right state (must be in serving state)
            if (checkSessionServing(sessionId))
            {
                PPLog.infoLog("Uploading session archive!");

                string mediaId = POICdnHelper.generateCdnKeyForSessionArchive(sessionId);
                POICdnHelper.uploadStrToQiniuCDN(mediaId, jsonHandler.Serialize(""));

                //Update the database given the media id
                Dictionary<string, object> conditions = new Dictionary<string, object>();
                conditions["id"] = sessionId;

                Dictionary<string, object> values = new Dictionary<string, object>();
                values["media_id"] = mediaId;

                dbManager.updateTable("session", values, conditions);

                return true;
            }
            else
            {
                return false;
            }
        }

        public void uploadSessionArchive(string sessionId)
        {
            PPLog.infoLog("[POIProxyInteractiveMsgHandler uploadSessionArchive] Uploading session archive!");

            string mediaId = POICdnHelper.generateCdnKeyForSessionArchive(sessionId);
            POICdnHelper.uploadStrToQiniuCDN(mediaId, 
                jsonHandler.Serialize(POIProxySessionManager.Instance.getSessionArchive(sessionId))
            );

            //Update the database given the media id
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["media_id"] = mediaId;

            dbManager.updateTable("session", values, conditions);
        }

        public string duplicateInteractiveSession(string sessionId, double timestamp)
        {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;
            DataRow result = dbManager.selectSingleRowFromTable("session", null, conditions);

            if (result != null)
            {
                Dictionary<string, object> values = new Dictionary<string, object>();

                values["type"] = "interactive";
                values["presId"] = result["presId"];
                values["creator"] = result["creator"];
                values["create_at"] = timestamp;
                values["status"] = "open";

                return dbManager.insertIntoTable("session", values);
            }
            else
            {
                PPLog.infoLog("In duplicate session, cannot find original session");
                return (-1).ToString();
            }
        }

        

        public void updateQuestionDescription(string sessionId, string description)
        {
            POIProxySessionManager.Instance.updateSessionInfo(sessionId,
                new Dictionary<string,string>{ { "description", description } }
            );
        }

        public void updateQuestionMediaId(string sessionId, string mediaId)
        {
            POIProxySessionManager.Instance.updateSessionInfo(sessionId,
                new Dictionary<string, string> { { "cover", mediaId } }
            );
        }

        

        public void endInteractiveSession(string msgId, string userId, string sessionId)
        {
            if (POIProxySessionManager.Instance.checkPrivateTutoring(sessionId))
            {
                //Upload the session archive
                uploadSessionArchive(sessionId);

                //Turn the session to waiting status for user rating
                updateSessionStatusWithEnding(sessionId);
            }
            

            //Archive the session end event
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            POIInteractiveEvent endEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "session_ended",
                EventId = msgId,
                UserId = userId,
                Timestamp = timestamp
            };

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, endEvent);
        }

        public void rateInteractiveSession(Dictionary<string,string> msgInfo)
        {
            string msgId = msgInfo["msgId"];
            string userId = msgInfo["userId"];
            string sessionId = msgInfo["sessionId"];

            List<string> userList = POIProxySessionManager.Instance.getUsersBySessionId(sessionId);
            //POIProxySessionManager.Instance.unsubscribeSession(sessionId, userId);
            string tutorId = null;
            if (userList.Count != 0)
            {
                userList.Remove(userId);
                tutorId = userList[0];
            }

            int rating = Convert.ToInt32(msgInfo["rating"]);
            if (POIProxySessionManager.Instance.checkPrivateTutoring(sessionId))
            {
                //Check if the session is in serving status
                if (checkSessionServing(sessionId))
                {
                    //Session end initated by rating event
                    updateSessionStatusWithRating(sessionId, rating, true);

                    
                }
                else
                {
                    PPLog.debugLog("rateInteractiveSession checkSessionServing not");
                    //Session end initiated by end event
                    updateSessionStatusWithRating(sessionId, rating, false);
                }
            }
            else
            {
                if (checkSessionOpen(sessionId))
                {
                    cancelInteractiveSession(msgInfo);
                }
                else
                {
                    //In group session, there is no session end waiting, so end is initiated by rating
                    updateSessionStatusWithRating(sessionId, rating, true);
                }
            }

            //Archive the session rating event
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            POIInteractiveEvent rateEvent = new POIInteractiveEvent
            {
                EventType = "session_rated",
                EventId = msgId,
                UserId = userId,
                Timestamp = timestamp,
                Data = new Dictionary<string, string>
                {
                    {"rating", rating.ToString()}
                }
            };

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, rateEvent);

            //Update the session info with rating
            POIProxySessionManager.Instance.updateSessionInfo(sessionId, new Dictionary<string, string>
            {
                {"rating", rating.ToString()}
            });

            
            //Upload the session archive
            uploadSessionArchive(sessionId);

            //Update the answer activity
            if (tutorId != null) {
                updateAnswerActivity(tutorId, sessionId, rating);

                //update user score.
                Dictionary<string, object> userConditions = new Dictionary<string, object>();
                userConditions["user_id"] = tutorId;

                List<string> userCols = new List<string>();
                userCols.Add("interactive_score");

                DataRow userResult = getByUserId(userConditions, userCols, "user_score");

                updateByUserId(tutorId, "interactive_score", (int)userResult["interactive_score"] + rating * 10, "user_score");
                POIProxySessionManager.Instance.updateUserScoreRanking(tutorId, rating * 10);
            }
        }

        //Functions for sending messages
        public void textMsgReceived(string msgId, string userId, string sessionId, string message, double timestamp, string customerId)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                EventType = "text",
                EventId = msgId,
                UserId = userId,
                Timestamp = timestamp,
                Message = message,
                CustomerId = customerId,
            };
            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEvent);
        }

        public void imageMsgReceived(string msgId, string userId, string sessionId, string mediaId, double timestamp, string customerId)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "image",
                EventId = msgId,
                MediaId = mediaId,
                UserId = userId,
                Timestamp = timestamp,
                CustomerId = customerId,
            };

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEvent);
        }

        public void voiceMsgReceived(string msgId, string userId, string sessionId, string mediaId, double timestamp, float mediaDuration, string customerId)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "voice",
                EventId = msgId,
                MediaId = mediaId,
                MediaDuration = mediaDuration,
                UserId = userId,
                Timestamp = timestamp,
                CustomerId = customerId,
            };

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEvent);
        }

        public void illustrationMsgReceived(string msgId, string userId, string sessionId, string mediaId, double timestamp, string customerId)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "illustration",
                EventId = msgId,
                MediaId = mediaId,
                UserId = userId,
                Timestamp = timestamp,
                CustomerId = customerId,
            };

            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEvent);

        }

        public void systemMsgReceived(string msgId, string userId, string sessionId, string message, double timestamp)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent { 
                EventType = "system",
                EventId = msgId,
                UserId = userId,
                Message = message,
                Timestamp = timestamp,
            };
            POIProxySessionManager.Instance.archiveSessionEvent(sessionId, poiEvent);
        }

        public string getDeviceTypeByUserId(string userId)
        {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["uid"] = userId;

            List<string> cols = new List<string>();
            cols.Add("type");

            DataTable result = dbManager.selectFromTable("user_device", cols, conditions);
            if (result.Rows.Count > 0)
            {
                return result.Rows[0]["type"].ToString();
            }
            else 
            {
                return "";
            }
        }

        public List<object> getMissedEventsInSession(string sessionId, double timestamp)
        {
            //PPLog.debugLog("[POIProxyInteractiveMsgHandler getMissedEventsInSession] Getting missed event for session : " + sessionId);
            var missedEvents = new List<object>();

            try
            {
                var eventList = POIProxySessionManager.Instance.getSessionEventList(sessionId, false);
                
                //Get all event with timestamp larger than the given timestamp
                if (eventList != null)
                {
                    for (int i = 0; i < eventList.Count; i++)
                    {
                        if (eventList[i].Timestamp > timestamp)
                        {
                            Dictionary<string, object> message = new Dictionary<string, object>();
                            
                            int eventType = (int)POIGlobalVar.resource.SESSIONS;

                            message["msgId"] = eventList[i].EventId;
                            message["userId"] = eventList[i].UserId;
                            message["sessionId"] = sessionId;
                            message["timestamp"] = eventList[i].Timestamp;
                            
                            if (eventList[i].EventType == "session_created") {
                                message["sessionType"] = POIGlobalVar.sessionType.CREATE;
                                message["mediaId"] = eventList[i].MediaId;
                                var sessionInfo = POIProxySessionManager.Instance.getSessionInfo(sessionId);
                                message["description"] = sessionInfo.ContainsKey("description") ? sessionInfo["description"] : "";
                                message["presId"] = sessionInfo.ContainsKey("pres_id") ? sessionInfo["pres_id"] : "0";
                            }
                            else if (eventList[i].EventType == "session_joined") {
                                message["sessionType"] = POIGlobalVar.sessionType.JOIN;
                                message["message"] = eventList[i].Message;
                            }
                            else if (eventList[i].EventType == "session_cancelled") {
                                message["sessionType"] = POIGlobalVar.sessionType.CANCEL;
                            }
                            else if (eventList[i].EventType == "session_ended") {
                                message["sessionType"] = POIGlobalVar.sessionType.END;
                            }
                            else if (eventList[i].EventType == "session_rated") {
                                message["sessionType"] = POIGlobalVar.sessionType.RATING;
                                var sessionInfo = POIProxySessionManager.Instance.getSessionInfo(sessionId);
                                message["rating"] = sessionInfo.ContainsKey("rating") ? sessionInfo["rating"] : "0"; 
                            }

                            else if (eventList[i].EventType == "text") {
                                message["msgType"] = POIGlobalVar.messageType.TEXT;
                                eventType = (int)POIGlobalVar.resource.MESSAGES;
                            }
                            else if (eventList[i].EventType == "voice") {
                                message["msgType"] = POIGlobalVar.messageType.VOICE;
                                eventType = (int)POIGlobalVar.resource.MESSAGES;
                            }
                            else if (eventList[i].EventType == "image") {
                                message["msgType"] = POIGlobalVar.messageType.IMAGE;
                                eventType = (int)POIGlobalVar.resource.MESSAGES;
                            }
                            else if (eventList[i].EventType == "illustration") {
                                message["msgType"] = POIGlobalVar.messageType.ILLUSTRATION;
                                eventType = (int)POIGlobalVar.resource.MESSAGES;
                            }
                            else {
                                message["msgType"] = POIGlobalVar.sessionType.GET;
                            }

                            if (eventType == (int)POIGlobalVar.resource.SESSIONS)
                            {
                                message["resource"] = POIGlobalVar.resource.SESSIONS;
                                message["userInfo"] = jsonHandler.Serialize(POIProxySessionManager.Instance.getUserInfo(eventList[i].UserId));
                            }
                            else { 
                                message["resource"] = POIGlobalVar.resource.MESSAGES;
                                message["message"] = eventList[i].Message;
                                message["mediaId"] = eventList[i].MediaId;
                                message["mediaDuration"] = eventList[i].MediaDuration;
                            }
                            

                            missedEvents.Add(message);
                        }
                    }
                }
                
                //PPLog.debugLog("Missed events is " + jsonHandler.Serialize(missedEvents));
            }
            catch (Exception e)
            {
                PPLog.errorLog(e.Message);
            }
           

            return missedEvents;
        }

    }
}
