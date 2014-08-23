using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;

namespace POIProxy
{
    public class POISessionRegistery
    {
        Dictionary<POIUser, POISession> sessionUserRegistery = new Dictionary<POIUser, POISession>();
        Dictionary<int, List<POISession>> sessionContentRegistery = new Dictionary<int, List<POISession>>();
        Dictionary<int, POISession> sessionIdRegistery = new Dictionary<int, POISession>();

        public POISession GetSessionByUser(POIUser user)
        {
            if (sessionUserRegistery.ContainsKey(user))
            {
                return sessionUserRegistery[user];
            }
            else
                return null;
        }

        public POISession GetSessionById(int sessionId)
        {
            if (sessionIdRegistery.ContainsKey(sessionId))
            {
                return sessionIdRegistery[sessionId];
            }
            else
                return null;
        }

        public List<POISession> GetSessionByContent(int contentId)
        {
            if (sessionContentRegistery.ContainsKey(contentId))
            {
                return sessionContentRegistery[contentId];
            }
            else
                return null;
        }

        public void AddSession(POIUser user, POISession session)
        {
            POISessionInfo info = session.Info;

            int contentId = info.contentId;
            if (!sessionContentRegistery.ContainsKey(contentId))
            {
                sessionContentRegistery[contentId] = new List<POISession>();
            }

            sessionUserRegistery[user] = session;
            sessionContentRegistery[contentId].Add(session);
            sessionIdRegistery[info.sessionId] = session;
        }

        public void RegisterSession(POIUser user, int sessionId)
        {
            if (sessionIdRegistery.ContainsKey(sessionId))
            {
                sessionUserRegistery[user] = sessionIdRegistery[sessionId];
            }
            else
            {
                POIGlobalVar.POIDebugLog("No such session exists!");
            }
        }

        public void DeRegisterSession(POIUser user)
        {
            sessionUserRegistery[user] = null;
        }

        public void RemoveSession(POIUser user)
        {
            if (sessionUserRegistery.ContainsKey(user))
            {
                POISession session = sessionUserRegistery[user];
                POISessionInfo info = session.Info;

                if (sessionIdRegistery.ContainsKey(info.sessionId))
                {
                    sessionIdRegistery.Remove(info.sessionId);
                }
                else
                {
                    POIGlobalVar.POIDebugLog("No session associated with such session id");
                }

                if (sessionContentRegistery.ContainsKey(info.contentId))
                {
                    sessionContentRegistery[info.contentId].Remove(session);
                }
                else
                {
                    POIGlobalVar.POIDebugLog("No session associated with such content id");
                }

                sessionUserRegistery.Remove(user);
            }
            else
            {
                POIGlobalVar.POIDebugLog("No session associated with the user");
            }
        }
    }
}
