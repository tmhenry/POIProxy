using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using Microsoft.AspNet.SignalR;
using System.Data;
using System.Web.Script.Serialization;

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
            var studentInfo = POIProxySessionManager.getUserInfo(userId);
            var sessionInfo = POIProxySessionManager.getSessionInfo(sessionId);

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
            var sessionInfo = POIProxySessionManager.getSessionInfo(sessionId);
            var studentInfo = POIProxySessionManager.getUserInfo(sessionInfo["creator"]);
            var tutorInfo = POIProxySessionManager.getUserInfo(userId);

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

            var sessionInfo = POIProxySessionManager.getSessionInfo(sessionId);
            var studentInfo = POIProxySessionManager.getUserInfo(sessionInfo["creator"]);
            var tutorInfo = POIProxySessionManager.getUserInfo(userId);

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

        public Tuple<string,string> createInteractiveSession(string userId, string mediaId, 
            string desc, string accessType = "private")
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
            values["presId"] = presId;
            values["creator"] = userId;
            values["create_at"] = timestamp;
            values["status"] = "created";

            string sessionId = dbManager.insertIntoTable("session", values);

            //Insert record into the database for the user to session relationship
            addUserToSessionRecord(userId, sessionId);

            //Get the information about the activity
            var userInfo = POIProxySessionManager.getUserInfo(userId);
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
                POIProxySessionManager.updateSessionInfo(sessionId, infoDict);
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
                MediaId = "",
                UserId = userId,
                Timestamp = timestamp,
                Message = "",
                Data = infoDict
            };

            POIProxySessionManager.archiveSessionEvent(sessionId, poiEvent, timestamp);

            //Subscribe the user to the session
            POIProxySessionManager.subscribeSession(sessionId, userId);
            
            //Insert the question activity into the activity table
            addQuestionActivity(userId, sessionId);

            return new Tuple<string,string>(presId, sessionId);
        }

        public void joinInteractiveSession(string userId, string sessionId, double timestamp)
        {
            //add the current user into the session table
            addUserToSessionRecord(userId, sessionId);

            //Turn the session to serving status
            updateSessionStatusWithTutorJoin(userId, sessionId);

            //Archive the session join information
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                EventType = "session_joined",
                MediaId = "",
                UserId = userId,
                Timestamp = timestamp,
                Message = "",
                Data = POIProxySessionManager.getUserInfo(userId)
            };

            POIProxySessionManager.archiveSessionEvent(sessionId, poiEvent, timestamp);

            //Subscribe the user to the session
            POIProxySessionManager.subscribeSession(sessionId, userId);

            //Add the activity record
            addAnswerActivity(userId, sessionId);
        }

        public void cancelInteractiveSession(string userId, string sessionId)
        {
            double timestamp = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            //Set the status to cancelled
            updateSessionStatus(sessionId, "cancelled");

            //Upload the session archive to the qiniu cdn
            string mediaId = POICdnHelper.generateCdnKeyForSessionArchive(sessionId);
            POICdnHelper.uploadStrToQiniuCDN(mediaId, "");

            //Update the database given the media id
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["media_id"] = mediaId;

            dbManager.updateTable("session", values, conditions);

            //Unsubscribe the old session
            POIProxySessionManager.unsubscribeSession(sessionId, userId);

            //Archive the cancel event
            POIInteractiveEvent cancelEvent = new POIInteractiveEvent
            {
                EventType = "session_cancelled",
                UserId = userId,
                Timestamp = timestamp,
            };

            POIProxySessionManager.archiveSessionEvent(sessionId, cancelEvent, timestamp);
        }

        public void reraiseInteractiveSession(string userId, string sessionId, string newSessionId, double timestamp)
        {
            //Set the status to cancelled for the initial session
            updateSessionStatus(sessionId, "cancelled");

            //Unsubscribe the old session
            POIProxySessionManager.unsubscribeSession(sessionId, userId);

            //Archive the cancel event
            POIInteractiveEvent cancelEvent = new POIInteractiveEvent
            {
                EventType = "session_cancelled",
                UserId = userId,
                Timestamp = timestamp,
            };

            POIProxySessionManager.archiveSessionEvent(sessionId, cancelEvent, timestamp);

            Dictionary<string, string> info = POIProxySessionManager.getSessionInfo(sessionId);

            //Upload the session archive
            string mediaId = POICdnHelper.generateCdnKeyForSessionArchive(sessionId);
            POICdnHelper.uploadStrToQiniuCDN(mediaId, jsonHandler.Serialize(""));

            //Update the database given the media id
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["media_id"] = mediaId;

            dbManager.updateTable("session", values, conditions);

            //Update the new session info
            POIProxySessionManager.updateSessionInfo(sessionId,
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
            POIProxySessionManager.subscribeSession(newSessionId, userId);

            //Archive the create event
            POIInteractiveEvent createEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "session_created",
                MediaId = "",
                UserId = userId,
                Timestamp = timestamp,
                Message = "",
                Data = POIProxySessionManager.getSessionInfo(newSessionId)
            };

            POIProxySessionManager.archiveSessionEvent(newSessionId, createEvent, timestamp);
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
                jsonHandler.Serialize(POIProxySessionManager.getSessionArchive(sessionId))
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
                values["status"] = "created";

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
            POIProxySessionManager.updateSessionInfo(sessionId,
                new Dictionary<string,string>{ { "description", description } }
            );
        }

        public void updateQuestionMediaId(string sessionId, string mediaId)
        {
            POIProxySessionManager.updateSessionInfo(sessionId,
                new Dictionary<string, string> { { "cover", mediaId } }
            );
        }

        

        public void endInteractiveSession(string userId, string sessionId)
        {
            if (POIProxySessionManager.checkPrivateTutoring(sessionId))
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
                UserId = userId,
                Timestamp = timestamp
            };

            POIProxySessionManager.archiveSessionEvent(sessionId, endEvent, timestamp);
        }

        public void rateInteractiveSession(string userId, string sessionId, int rating)
        {
            PPLog.debugLog("rateInteractiveSession: userId: "+userId+" sessionId: "+sessionId+ " rating: "+rating);
            if (POIProxySessionManager.checkPrivateTutoring(sessionId))
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
                    cancelInteractiveSession(userId, sessionId);
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
                UserId = userId,
                Timestamp = timestamp,
                Data = new Dictionary<string, string>
                {
                    {"rating", rating.ToString()}
                }
            };

            POIProxySessionManager.archiveSessionEvent(sessionId, rateEvent, timestamp);

            //Update the session info with rating
            POIProxySessionManager.updateSessionInfo(sessionId, new Dictionary<string, string>
            {
                {"rating", rating.ToString()}
            });

            
            //Upload the session archive
            uploadSessionArchive(sessionId);

            //Update the answer activity
            updateAnswerActivity(userId, sessionId, rating);
        }

        //Functions for sending messages
        public void textMsgReceived(string userId, string sessionId, string message, double timestamp)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                EventType = "text",
                UserId = userId,
                Timestamp = timestamp,
                Message = message
            };

            POIProxySessionManager.archiveSessionEvent(sessionId, poiEvent, timestamp);
        }

        public void imageMsgReceived(string userId, string sessionId, string mediaId, double timestamp)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "image",
                MediaId = mediaId,
                UserId = userId,
                Timestamp = timestamp,
            };

            POIProxySessionManager.archiveSessionEvent(sessionId, poiEvent, timestamp);
        }

        public void voiceMsgReceived(string userId, string sessionId, string mediaId, double timestamp)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "voice",
                MediaId = mediaId,
                UserId = userId,
                Timestamp = timestamp,
            };

            POIProxySessionManager.archiveSessionEvent(sessionId, poiEvent, timestamp);
        }

        public void illustrationMsgReceived(string userId, string sessionId, string mediaId, double timestamp)
        {
            POIInteractiveEvent poiEvent = new POIInteractiveEvent
            {
                //EventIndex = EventList.Count,
                EventType = "illustration",
                MediaId = mediaId,
                UserId = userId,
                Timestamp = timestamp,
            };

            POIProxySessionManager.archiveSessionEvent(sessionId, poiEvent, timestamp);
        }

        public List<POIInteractiveEvent> getMissedEventsInSession(string sessionId, double timestamp)
        {
            //PPLog.debugLog("[POIProxyInteractiveMsgHandler getMissedEventsInSession] Getting missed event for session : " + sessionId);
            var missedEvents = new List<POIInteractiveEvent>();

            try
            {
                var eventList = POIProxySessionManager.getSessionEventList(sessionId);

                //Get all event with timestamp larger than the given timestamp
                if (eventList != null)
                {
                    for (int i = 0; i < eventList.Count; i++)
                    {
                        if (eventList[i].Timestamp > timestamp)
                        {
                            missedEvents.Add(eventList[i]);
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
