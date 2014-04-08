﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using Microsoft.AspNet.SignalR;
using System.Data;
using System.Web.Script.Serialization;

using POILibCommunication;
using System.Threading.Tasks;

namespace POIProxy.Handlers
{
    public class POIProxyInteractiveMsgHandler 
    {
        IHubContext hubContext = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
        POIProxyDbManager dbManager = POIProxyDbManager.Instance;
        JavaScriptSerializer jsonHandler = new JavaScriptSerializer();

        ConcurrentDictionary<string, POIInteractiveSessionArchive> sessionArchives = 
            new ConcurrentDictionary<string, POIInteractiveSessionArchive>();

        #region functions communicating with database
        
        //Functions for communicating with mySQL database
        public List<string> getReceipientList(string userId)
        {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["uid"] = userId;
            List<string> receipients = new List<string>();

            DataTable matchedUsers = dbManager.selectFromTable("user_match", null, conditions);
            if (matchedUsers.Rows.Count > 0)
            {
                foreach (DataRow row in matchedUsers.Rows)
                {
                    receipients.Add(row["matched_uid"] as string);
                }
            }
            else
            {
                POIGlobalVar.POIDebugLog("Error in finding matched user id");
            }

            return receipients;
        }


        public bool checkIsTutor(string userId)
        {
            bool isTutor = false;

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = userId;
            conditions["accessRight"] = "tutor";

            List<string> cols = new List<string>();
            cols.Add("id");

            DataTable result = dbManager.selectFromTable("users", cols, conditions);

            if (result.Rows.Count > 0)
            {
                isTutor = true;
            }

            return isTutor;
        }

        public bool checkTutorIdle(string tutorId)
        {
            bool isIdle = false;

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["uid"] = tutorId;
            conditions["status"] = "idle";

            DataTable result = dbManager.selectFromTable("tutor_status", null, conditions);
            if (result.Rows.Count > 0)
            {
                isIdle = true;
            }

            return isIdle;
        }

        public void setTutorStatus(string tutorId, string status)
        {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            Dictionary<string, object> values = new Dictionary<string, object>();

            conditions["uid"] = tutorId;
            values["uid"] = tutorId;
            values["status"] = status;

            DataTable result = dbManager.selectFromTable("tutor_status", null, conditions);
            if (result.Rows.Count > 0)
            {
                dbManager.updateTable("tutor_status", values, conditions);
            }
            else
            {
                dbManager.insertIntoTable("tutor_status", values);
            }
        }

        public void setTutorIdle(string tutorId)
        {
            setTutorStatus(tutorId, "idle");
        }

        public void setTutorUnavailable(string tutorId)
        {
            setTutorStatus(tutorId, "unavailable");
        }

        public void resetTutorRelatedAssignment(string tutorId)
        {
            //remove talker relationship
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["uid"] = tutorId;
            dbManager.deleteFromTable("user_match", conditions);

            //remove receipient relationship
            conditions.Clear();
            conditions["matched_uid"] = tutorId;
            dbManager.deleteFromTable("user_match", conditions);
        }

        public bool checkUserExists(string userId)
        {
            bool userExists = false;

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = userId;

            List<string> cols = new List<string>();
            cols.Add("id");

            DataTable result = dbManager.selectFromTable("users", cols, conditions);

            if (result.Rows.Count > 0)
            {
                userExists = true;
            }

            return userExists;
        }

        public string getUserName(string userId)
        {
            string userName = "";

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = userId;

            List<string> cols = new List<string>();
            cols.Add("username");

            DataTable userInfo = dbManager.selectFromTable("users", cols, conditions);

            if (userInfo.Rows.Count > 0)
            {
                userName = userInfo.Rows[0]["username"] as string;
            }

            return userName;
        }

        public void assignMatchedUser(string userId, string matchedUid)
        {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            Dictionary<string, object> values = new Dictionary<string, object>();

            conditions["uid"] = userId;
            values["matched_uid"] = matchedUid;
            values["uid"] = userId;

            DataTable result = dbManager.selectFromTable("user_match", null, conditions);
            if (result.Rows.Count > 0)
            {
                //Update
                dbManager.updateTable("user_match", values, conditions);
            }
            else
            {
                //Insert
                dbManager.insertIntoTable("user_match", values);
            }
        }

        public void unassignMatchedUser(string userId, string matchedUid)
        {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["uid"] = userId;
            conditions["matched_uid"] = matchedUid;

            dbManager.deleteFromTable("user_match", conditions);
        }

        //Not completed
        public void registerDeviceToUser(string userId, string token, string tokenType)
        {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["uid"] = userId;

            DataTable deviceInfo = dbManager.selectFromTable("user_device", null, conditions);

            if (deviceInfo.Rows.Count == 0)
            {
                List<string> deviceList = new List<string>();
                deviceList.Add(token);

                Dictionary<string, List<string>> tokens = new Dictionary<string, List<string>>();
                tokens[tokenType] = deviceList;

                Dictionary<string, object> values = new Dictionary<string, object>();
                values["uid"] = userId;
                values["tokens"] = jsonHandler.Serialize(tokens);

                dbManager.insertIntoTable("user_device", values);
            }
            else if (deviceInfo.Rows.Count == 1)
            {
                //To be completed later
            }
            else
            {
                POIGlobalVar.POIDebugLog("Error in register token to user");
            }

        }

        #endregion

        public List<string> getUsersInSession(string sessionId, string excludedUser = "")
        {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["type"] = "session";
            conditions["content_id"] = sessionId;
            conditions["user_right"] = 4;

            List<string> cols = new List<string>();
            cols.Add("user_id");

            DataTable result = dbManager.selectFromTable("user_right", cols, conditions);
            List<string> userList = new List<string>();
            foreach (DataRow row in result.Rows)
            {
                userList.Add(row["user_id"] as string);
            }

            if (excludedUser != "")
            {
                userList.Remove(excludedUser);
            }

            return userList;
        }

        public bool checkUserInSession(string userId, string sessionId)
        {
            bool inSession = false;

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["user_id"] = userId;
            conditions["type"] = "session";
            conditions["content_id"] = sessionId;
            conditions["user_right"] = 4;

            List<string> cols = new List<string>();
            cols.Add("id");

            DataTable result = dbManager.selectFromTable("user_right", cols, conditions);
            if (result.Rows.Count > 0)
            {
                inSession = true;
            }

            return inSession;
        }
        
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

        public void addQuestionActivity(string userId, string sessionId, string info)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["user_id"] = userId;
            values["type"] = "int_session_question";
            values["content_id"] = sessionId;
            values["create_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            values["data"] = info;

            dbManager.insertIntoTable("activity", values);
        }

        public void addAnswerActivity(string userId, string sessionId, string info)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["user_id"] = userId;
            values["type"] = "int_session_answer";
            values["content_id"] = sessionId;
            values["data"] = info;
            values["create_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);

            dbManager.insertIntoTable("activity", values);
        }

        public void updateAnswerActivity(string userId, string sessionId, int rating)
        {
            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["content_id"] = sessionId;
            conditions["type"] = "int_session_answer";

            List<string> cols = new List<string>();
            cols.Add("data");

            DataTable result = dbManager.selectFromTable("activity", cols, conditions);
            if (result.Rows.Count >= 1)
            {
                string infoStr = result.Rows[0]["data"] as string;
                Dictionary<string, string> infoDict = jsonHandler.Deserialize<Dictionary<string, string>>(infoStr);
                infoDict["rating"] = rating.ToString();
                infoStr = jsonHandler.Serialize(infoDict);

                Dictionary<string, object> values = new Dictionary<string, object>();
                values["data"] = infoStr;

                dbManager.updateTable("activity", values, conditions);
            }
            else
            {
                POIGlobalVar.POIDebugLog("Cannot find related answer activity!");
            }
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
            var session = getArchiveBySessionId(sessionId);
            return session.joinSessionIfOpen();
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
                POIGlobalVar.POIDebugLog("found session open " + result.Rows[0]["id"]);
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

        public Tuple<string,string> createInteractiveSession(string userId, string mediaId, string desc)
        {
            //Create interactive presentation
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["user_id"] = userId;
            values["type"] = "interactive";
            values["course_id"] = -1;
            values["description"] = desc;
            values["create_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            values["media_id"] = mediaId;

            string presId = dbManager.insertIntoTable("presentation", values);

            values.Clear();
            values["type"] = "interactive";
            values["presId"] = presId;
            values["creator"] = userId;
            values["create_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
            values["status"] = "created";

            string sessionId = dbManager.insertIntoTable("session", values);

            //Insert record into the database for the user to session relationship
            addUserToSessionRecord(userId, sessionId);

            //Get the information about the activity
            Dictionary<string, string> infoDict = new Dictionary<string, string>();
            infoDict["session_id"] = sessionId;
            infoDict["student_id"] = userId;
            infoDict["description"] = desc;
            infoDict["cover"] = mediaId;
            infoDict["status"] = "open";
            infoDict["tutor_id"] = null;
            infoDict["tutor_avatar"] = null;
            infoDict["tutor_name"] = null;

            //Search for user name of the student
            Dictionary<string, object> condition = new Dictionary<string, object>();
            condition["id"] = userId;
            DataTable result = dbManager.selectFromTable("users", null, condition);
            if (result.Rows.Count == 1)
            {
                DataRow row = result.Rows[0];
                infoDict["student_avatar"] = row["avatar"] as string;
                infoDict["student_name"] = row["username"] as string;
            }

            //Insert the question activity into the activity table
            addQuestionActivity(userId, sessionId, jsonHandler.Serialize(infoDict));

            //Create session archive and add the currrent user to the user list
            initSessionArchive(infoDict);

            return new Tuple<string,string>(presId, sessionId);
        }

        public void initSessionArchive(Dictionary<string,string> info)
        {
            //Create session archive and add the currrent user to the user list
            POIInteractiveSessionArchive archive = new POIInteractiveSessionArchive(info);
            sessionArchives[archive.SessionId] = archive;
        }

        private Dictionary<string, string> getArchiveInfoFromDb(string sessionId)
        {
            //Get the pres id from session table
            Dictionary<string, object> conditions = new Dictionary<string, object> 
            { 
                {"id", sessionId}
            };

            var sessionRecord = dbManager.selectSingleRowFromTable("session", null, conditions);
            var presId = sessionRecord["presId"];
            string userId = sessionRecord["creator"] as string;
            string tutorId = sessionRecord["tutor"] as string;

            conditions.Clear();
            conditions["pid"] = presId;
            var presRecord = dbManager.selectSingleRowFromTable("presentation", null, conditions);

            Dictionary<string, string> info = new Dictionary<string, string>
            {
                {"session_id", sessionId},
                {"student_id", userId},
                {"tutor_id",  tutorId},
                {"cover", presRecord["media_id"] as string},
                {"description", presRecord["description"] as string},
                {"status", sessionRecord["status"] as string}
            };

            if (userId != null)
            {
                conditions.Clear();
                conditions["id"] = userId;
                var userRecord = dbManager.selectSingleRowFromTable("users", null, conditions);

                info["student_avatar"] = userRecord["avatar"] as string;
                info["student_name"] = userRecord["username"] as string;
            }

            if (tutorId != null)
            {
                conditions.Clear();
                conditions["id"] = tutorId;
                var tutorRecord = dbManager.selectSingleRowFromTable("users", null, conditions);

                info["tutor_avatar"] = tutorRecord["avatar"] as string;
                info["tutor_name"] = tutorRecord["username"] as string;
            }
            
            return info;
        }

        public async Task<bool> checkAndProcessArchiveDuringSessionEnd(string sessionId)
        {
            POIGlobalVar.POIDebugLog("Session id is " + sessionId);

            //Check if the session is in the right state (must be in serving state)
            if (checkSessionServing(sessionId))
            {
                POIGlobalVar.POIDebugLog("Uploading session archive!");

                var session = getArchiveBySessionId(sessionId);

                //Prepare the archive and upload to the cloud
                string mediaId = await POIContentServerHelper.uploadJsonStrToQiniuCDN(
                    jsonHandler.Serialize(session)
                );

                //Update the database given the media id
                Dictionary<string, object> conditions = new Dictionary<string, object>();
                conditions["id"] = sessionId;

                Dictionary<string, object> values = new Dictionary<string, object>();
                values["media_id"] = mediaId;

                dbManager.updateTable("session", values, conditions);

                //Remove the session archive in the memory
                POIInteractiveSessionArchive archive;
                sessionArchives.TryRemove(sessionId, out archive);

                return true;
            }
            else
            {
                return false;
            }
        }

        public Dictionary<string, object> getUserInfoById(string userId)
        {
            Dictionary<string, object> userInfo = new Dictionary<string, object>();

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            List<string> cols = new List<string>();

            conditions["id"] = userId;
            cols.Add("username");
            cols.Add("avatar");

            DataRow user = dbManager.selectSingleRowFromTable("users", cols, conditions);
            if(user != null)
            {
                userInfo["username"] = user["username"];
                userInfo["avatar"] = user["avatar"];

                //Find the user profile 
                conditions.Clear();
                cols.Clear();
                conditions["user_id"] = userId;
                cols.Add("school");
                cols.Add("department");
                cols.Add("rating");

                DataRow profile = dbManager.selectSingleRowFromTable("user_profile", cols, conditions);
                if(profile != null)
                {
                    userInfo["rating"] = profile["rating"];
                    POIGlobalVar.POIDebugLog("School is " + profile["school"]);
                    POIGlobalVar.POIDebugLog("Dept is " + profile["department"]);

                    conditions.Clear();
                    cols.Clear();

                    conditions["sid"] = profile["school"];
                    cols.Add("name");
                    DataRow school = dbManager.selectSingleRowFromTable("school", cols, conditions);

                    if (school != null)
                    {
                        userInfo["school"] = school["name"];
                    }

                    conditions.Clear();
                    cols.Clear();

                    conditions["did"] = profile["department"];
                    cols.Add("name");
                    DataRow dept = dbManager.selectSingleRowFromTable("department", cols, conditions);

                    if (dept != null)
                    {
                        userInfo["department"] = dept["name"];
                    }
               }
            }
            
            return userInfo;
        }

        public string duplicateInteractiveSession(string sessionId)
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
                values["create_at"] = POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
                values["status"] = "created";

                return dbManager.insertIntoTable("session", values);
            }
            else
            {
                return (-1).ToString();
            }
        }

        public void reraiseInteractiveSession(string userId, string sessionId, string newSessionId)
        {
            //Set the status to cancelled for the initial session
            updateSessionStatus(sessionId, "cancelled");

            //Remove the initial session archive and insert the new archive
            if (sessionArchives.ContainsKey(sessionId))
            {
                POIInteractiveSessionArchive archive;
                sessionArchives.TryRemove(sessionId, out archive);

                //Initialize the archive for the new session
                archive.Info["session_id"] = newSessionId;
                initSessionArchive(archive.Info);
            }
            else
            {
                //No archive exists in memory, read it from database
                Dictionary<string,string> archiveInfo = getArchiveInfoFromDb(sessionId);
                archiveInfo["session_id"] = newSessionId;
                initSessionArchive(archiveInfo);
            }
        }

        public POIInteractiveSessionArchive joinInteractiveSession(string userId, string sessionId)
        {
            //add the current user into the session table
            addUserToSessionRecord(userId, sessionId);

            //Turn the session to serving status
            updateSessionStatusWithTutorJoin(userId, sessionId);

            POIInteractiveSessionArchive session = null;
            if (sessionArchives.ContainsKey(sessionId))
            {
                //Archive the session join event
                session = sessionArchives[sessionId];
                session.archiveSessionJoinedEvent(userId);
            }
            else
            {
                //Initialize the session archive
                session = new POIInteractiveSessionArchive(getArchiveInfoFromDb(sessionId));
            }

            //Add the activity record
            addAnswerActivity(userId, sessionId, jsonHandler.Serialize(session.Info));

            return session;
        }

        public POIInteractiveSessionArchive getArchiveBySessionId(string sessionId)
        {
            if (!sessionArchives.ContainsKey(sessionId))
            {
                initSessionArchive(getArchiveInfoFromDb(sessionId));
            }

            return sessionArchives[sessionId];
        }

        public void archiveSessionJoinedEvent(string userId, string sessionId)
        {
            var session = getArchiveBySessionId(sessionId);
            session.archiveSessionJoinedEvent(userId);
            session.updateSessionStatusServing();
        }

        public void updateQuestionDescription(string sessionId, string description)
        {
            var session = getArchiveBySessionId(sessionId);
            sessionArchives[sessionId].Info["description"] = description;
        }

        public void updateQuestionMediaId(string sessionId, string mediaId)
        {
            var session = getArchiveBySessionId(sessionId);
            sessionArchives[sessionId].Info["cover"] = mediaId;
        }

        public void cancelInteractiveSession(string userId, string sessionId)
        {
            //Set the status to cancelled
            updateSessionStatus(sessionId, "cancelled");

            //Remove the session archive
            if (sessionArchives.ContainsKey(sessionId))
            {
                POIInteractiveSessionArchive archive;
                sessionArchives.TryRemove(sessionId, out archive);
            }
        }

        public async Task endInteractiveSession(string userId, string sessionId)
        {
            //Check if the archive needs to be processed
            await checkAndProcessArchiveDuringSessionEnd(sessionId);

            //Turn the session to waiting status for user rating
            updateSessionStatusWithEnding(sessionId);
        }

        public async Task rateInteractiveSession(string userId, string sessionId, int rating)
        {
            //Check if the archive needs to be processed
            bool archiveProcessed = await checkAndProcessArchiveDuringSessionEnd(sessionId);

            //Turn the session to closed status and update the rating
            //Check if archive is processed by this event (if yes, session end is triggered by rating)
            if (archiveProcessed)
            {
                //Session end initated by rating event
                updateSessionStatusWithRating(sessionId, rating, true);
            }
            else
            {
                //Session end initiated by end event
                updateSessionStatusWithRating(sessionId, rating, false);
            }

            //Update the answer activity
            updateAnswerActivity(userId, sessionId, rating);
        }


        //Check if the message is duplicated
        public bool checkSessionMsgDuplicate(string sessionId, double msgTimestamp)
        {
            //POIGlobalVar.POIDebugLog("In check msg duplicate, timestamp is:" + msgTimestamp);
            var session = getArchiveBySessionId(sessionId);
            if (session.checkEventExists(msgTimestamp))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //Functions for sending messages
        public void textMsgReceived(string userId, string sessionId, string message, double timestamp)
        {
            var session = getArchiveBySessionId(sessionId);
            session.archiveTextEvent(userId, message, timestamp);
        }

        public void imageMsgReceived(string userId, string sessionId, string mediaId, double timestamp)
        {
            var session = getArchiveBySessionId(sessionId);
            session.archiveImageEvent(userId, mediaId, timestamp);
        }

        public void voiceMsgReceived(string userId, string sessionId, string mediaId, double timestamp)
        {
            var session = getArchiveBySessionId(sessionId);
            session.archiveVoiceEvent(userId, mediaId, timestamp);
        }

        public void illustrationMsgReceived(string userId, string sessionId, string mediaId, double timestamp)
        {
            var session = getArchiveBySessionId(sessionId);
            session.archiveIllustrationEvent(userId, mediaId, timestamp);
        }

        public List<POIInteractiveEvent> getMissedEventsInSession(string sessionId, double timestamp)
        {
            var session = getArchiveBySessionId(sessionId);
            var missedEvents = new List<POIInteractiveEvent>();
            var eventList = session.EventList;

            //Get all event with timestamp larger than the given timestamp
            for (int i = 0; i < eventList.Count; i++)
            {
                if (eventList[i].Timestamp > timestamp)
                {
                    missedEvents.Add(eventList[i]);
                }
            }

            POIGlobalVar.POIDebugLog(jsonHandler.Serialize(missedEvents));

            return missedEvents;
        }

    }
}
