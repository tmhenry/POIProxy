using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;
using Microsoft.AspNet.SignalR;
using System.Web.Script.Serialization;

using System.Threading;

namespace POIProxy
{
    public class POISessionManager : POISessionCtrlMsgCB
    {
        POISessionRegistery registery = new POISessionRegistery();
        POIOfflineSessionCache cache = new POIOfflineSessionCache();

        public POISessionRegistery Registery { get { return registery; } }
        public POIOfflineSessionCache Cache { get { return cache; } }

        public void StartSession(POIUser user, int contentId)
        {
            //Get a new session ID from the DNS server
            Dictionary<string, string> reqDict = new Dictionary<string, string>();
            reqDict["creator"] = user.UserID;
            reqDict["presId"] = contentId.ToString();
            reqDict["type"] = "public";
            int sessionId = POIWebService.CreateSession(reqDict);

            //Create a new session and add to registery
            POISession session = new POISession(user, sessionId, contentId);

            registery.AddSession(user, session);

            //Send back the session created message to the user
            POISessionMsg msg = new POISessionMsg();
            msg.initSessionCreatedMsg(session.Id);
            user.SendData(msg.getPacket(), ConType.TCP_CONTROL);
        }

        public void EndSession(POIUser user)
        {
            POISession session = registery.GetSessionByUser(user);
            if (session != null)
            {
                if (session.IsCommander(user))
                {
                    session.SessionEnd();
                    registery.RemoveSession(user);
                }
                else if (session.IsViewer(user))
                {
                    LeaveSession(user);
                }
            }
            
        }

        public void JoinSession(POIUser user, int sessionId)
        {
            POISession session = registery.GetSessionById(sessionId);
            if (session != null)
            {
                session.JoinAsViewer(user);
                registery.RegisterSession(user, sessionId);

                /*
                if (user.UserPrivilege == POIUser.Privilege.Commander)
                {
                    session.JoinAsCommander(user);
                    registery.RegisterSession(user, sessionId);
                }
                else if (user.UserPrivilege == POIUser.Privilege.Viewer)
                {
                    session.JoinAsViewer(user);
                    registery.RegisterSession(user, sessionId);
                }
                else
                {
                    POIGlobalVar.POIDebugLog("Not proper user privilege for user join operation");
                }*/
            }
        }

        public void LeaveSession(POIUser user)
        {
            POISession session = registery.GetSessionByUser(user);
            if (session != null)
            {
                if (session.IsCommander(user))
                {
                    session.LeaveAsCommander(user);
                }
                else if (session.IsViewer(user))
                {
                    session.LeaveAsViewer(user);
                }

                registery.DeRegisterSession(user);
            }
        }

        public void SessionAudioStart(POIUser user, double timeRef)
        {
            POISession session = registery.GetSessionByUser(user);
            if (session != null)
            {
                if (session.IsCommander(user))
                {
                    session.MdArchive.AudioTimeReference = timeRef;
                }
                else
                {
                    POIGlobalVar.POIDebugLog("Non-commander user cannot modify session info");
                }
            }
        }

        //A simple session id generator
        static int counter = 0;
        public int GenerateSessionId()
        {
            return counter++;
        }

        public List<POISessionInfo> GetSessionsByContent(int contentId)
        {
            List<POISessionInfo> result = new List<POISessionInfo>();
            List<POISession> curSessions = registery.GetSessionByContent(contentId);

            if (curSessions != null)
            {
                for (int i = 0; i < curSessions.Count; i++)
                {
                    result.Add(curSessions[i].Info);
                }
            }

            return result;
        }

        //Handler for session message
        public void sessionCtrlMsgReceived(POISessionMsg msg, POIUser user)
        {
            switch ((SessionCtrlType)msg.CtrlType)
            {
                case SessionCtrlType.Start: //Start session
                    StartSession(user, msg.ContentId);
                    break;
                case SessionCtrlType.End: //End session
                    EndSession(user);
                    break;
                case SessionCtrlType.Join: //Join session
                    JoinSession(user, msg.SessionId);
                    break;
                case SessionCtrlType.Leave: //Leave session
                    LeaveSession(user);
                    break;
                case SessionCtrlType.AudioStart: //Session audio start
                    SessionAudioStart(user, msg.Timestamp);
                    break;
            }
        }

        #region Utility functions for session management

        public void broadcastMessageToViewers(POIUser commander, POIMessage msg)
        {
            POIGlobalVar.POIDebugLog("Thread id is: " + System.Threading.Thread.CurrentThread.ManagedThreadId);

            //Get the current session
            POISession session = registery.GetSessionByUser(commander);
            POIGlobalVar.POIDebugLog("Commander id is: " + commander.UserID);

            if (session != null && session.IsCommander(commander))
            {
                //Send to the mobile viewers
                try
                {       
                    foreach (POIUser viewer in session.Viewers)
                    {
                        if (viewer != commander && viewer.Type != UserType.WEB)
                        {
                            POIGlobalVar.POIDebugLog("Sending to: " + viewer.UserID);
                            viewer.SendData(msg.getPacket(), ConType.TCP_CONTROL);
                        }
                    }
                }
                catch (Exception e)
                {
                    POIGlobalVar.POIDebugLog("Error in broadcasting messages to mobile viewers!");
                }
                
                //Forward to the web viewers
                var context = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
                JavaScriptSerializer jsHandler = new JavaScriptSerializer();
                context.Clients.Group(session.Id.ToString()).scheduleMsgHandling(jsHandler.Serialize(msg));
            }
            else
            {
                POIGlobalVar.POIDebugLog("Session is null or user is not commander when broadcasting msg to viewers!");
            }
        }

        public void sendMessageToCommanders(POIUser viewer, POIMessage msg)
        {
            //Get the current session
            POISession session = registery.GetSessionByUser(viewer);

            if (session != null && session.IsViewer(viewer))
            {
                //Send to the mobile commanders
                try
                {
                    foreach (POIUser commander in session.Commanders)
                    {
                        if (commander != viewer && commander.Type != UserType.WEB)
                        {
                            commander.SendData(msg.getPacket(), ConType.TCP_CONTROL);
                        }
                    }
                }
                catch (Exception e)
                {
                    POIGlobalVar.POIDebugLog("Error in sending messages to mobile commanders!");
                }
                
            }
            else
            {
                POIGlobalVar.POIDebugLog("Session is null or user is not a viewer when sending msg to commanders!");
            }
        }

        public void sendMessageToCommanders(int sessionId, POIMessage msg)
        {
            //Get the current session
            POISession session = registery.GetSessionById(sessionId);

            if (session != null)
            {
                //Send to the mobile commanders
                try
                {
                    foreach (POIUser commander in session.Commanders)
                    {
                        if (commander.Type != UserType.WEB)
                        {
                            commander.SendData(msg.getPacket(), ConType.TCP_CONTROL);
                        }
                    }
                }
                catch (Exception e)
                {
                    POIGlobalVar.POIDebugLog("Error in sending messages to mobile commanders!");
                }

            }
            else
            {
                POIGlobalVar.POIDebugLog("Session is null when sending msg to commanders!");
            }
        }


        //Notify all the web clients that the audio comment is ready
        public void notifyClientsAudioReady()
        {

        }

        #endregion
    }


}
