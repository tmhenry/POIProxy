using System;
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

            //Check if end_at attribute needs to be ended
            if(updateEndTime) values["end_at"] = values["rate_at"];

            Dictionary<string, object> conditions = new Dictionary<string, object>();
            conditions["id"] = sessionId;

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
                isInState = true;
            }

            return isInState;
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
            values["status"] = "open";

            string sessionId = dbManager.insertIntoTable("session", values);

            //Insert record into the database for the user to session relationship
            addUserToSessionRecord(userId, sessionId);

            //Get the information about the activity
            Dictionary<string, object> infoDict = new Dictionary<string, object>();
            infoDict["student_id"] = userId;
            infoDict["description"] = desc;
            infoDict["cover"] = mediaId;

            //Search for user name of the student
            Dictionary<string, object> condition = new Dictionary<string, object>();
            condition["id"] = userId;
            DataTable result = dbManager.selectFromTable("users", null, condition);
            if (result.Rows.Count == 1)
            {
                DataRow row = result.Rows[0];
                infoDict["student_avatar"] = row["avatar"];
                infoDict["student_name"] = row["username"];
            }
            

            string info = jsonHandler.Serialize(infoDict);

            //Insert the question activity into the activity table
            addQuestionActivity(userId, sessionId, info);

            //Create session archive and add the currrent user to the user list
            initSessionArchive(userId, sessionId, info);

            return new Tuple<string,string>(presId, sessionId);
        }

        public void initSessionArchive(string userId, string sessionId, string info)
        {
            //Create session archive and add the currrent user to the user list
            POIInteractiveSessionArchive archive = new POIInteractiveSessionArchive(sessionId, info);
            sessionArchives[sessionId.ToString()] = archive;
            
            //Add user to the list and archive the session_created event
            archive.addUserToUserList(userId);
            archive.archiveSessionCreatedEvent(userId);
        }

        public async Task<bool> checkAndProcessArchiveDuringSessionEnd(string sessionId)
        {
            //Check if the session is in the right state (must be in serving state)
            if (checkSessionServing(sessionId))
            {
                POIGlobalVar.POIDebugLog("Uploading session archive!");

                if (sessionArchives.ContainsKey(sessionId))
                {
                    //Remove the session archive in the memory
                    POIInteractiveSessionArchive archive;
                    sessionArchives.TryRemove(sessionId, out archive);

                    //Prepare the archive and upload to the cloud
                    string mediaId = await POIContentServerHelper.uploadJsonStrToQiniuCDN(
                        jsonHandler.Serialize(archive)
                    );

                    //Update the database given the media id
                    Dictionary<string, object> conditions = new Dictionary<string, object>();
                    conditions["id"] = sessionId;

                    Dictionary<string, object> values = new Dictionary<string, object>();
                    values["media_id"] = mediaId;

                    dbManager.updateTable("session", values, conditions);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public POIInteractiveSessionArchive joinInteractiveSession(string userId, string sessionId)
        {
            //add the current user into the session table
            addUserToSessionRecord(userId, sessionId);

            //Turn the session to serving status
            updateSessionStatusWithTutorJoin(userId, sessionId);

            

            if (sessionArchives.ContainsKey(sessionId))
            {
                //Archive the session join event
                var session = sessionArchives[sessionId];
                session.archiveSessionJoinedEvent(userId);

                //Add the activity record
                addAnswerActivity(userId, sessionId, session.Info);

                return session;
            }
            else
            {
                return null;
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

        

        //Functions for sending messages
        public void textMsgReceived(string userId, string sessionId, string message)
        {
            if (sessionArchives.ContainsKey(sessionId))
            {
                sessionArchives[sessionId].archiveTextEvent(userId, message);
            }
            else
            {
                POIGlobalVar.POIDebugLog("No archive related to session id " + sessionId);
            }
        }

        public void imageMsgReceived(string userId, string sessionId, string mediaId)
        {
            if (sessionArchives.ContainsKey(sessionId))
            {
                sessionArchives[sessionId].archiveImageEvent(userId, mediaId);
            }
            else
            {
                POIGlobalVar.POIDebugLog("No archive related to session id " + sessionId);
            }
        }

        public void voiceMsgReceived(string userId, string sessionId, string mediaId)
        {
            if (sessionArchives.ContainsKey(sessionId))
            {
                sessionArchives[sessionId].archiveVoiceEvent(userId, mediaId);
            }
            else
            {
                POIGlobalVar.POIDebugLog("No archive related to session id " + sessionId);
            }
        }

        public void illustrationMsgReceived(string userId, string sessionId, string mediaId)
        {
            if (sessionArchives.ContainsKey(sessionId))
            {
                sessionArchives[sessionId].archiveIllustrationEvent(userId, mediaId);
            }
            else
            {
                POIGlobalVar.POIDebugLog("No archive related to session id " + sessionId);
            }
        }

        public List<POIInteractiveEvent> getMissedEventsInSession(string sessionId, int eventIndex)
        {
            if (sessionArchives.ContainsKey(sessionId))
            {
                var missedEvents = new List<POIInteractiveEvent>();
                var eventList = sessionArchives[sessionId].EventList;
                
                for (int i = eventIndex + 1; i < eventList.Count; i++)
                {
                    missedEvents.Add(eventList[i]);
                }

                return missedEvents;
            }
            else
            {
                POIGlobalVar.POIDebugLog("No archive related to session id " + sessionId);
                return null;
            }
        }

    }
}
