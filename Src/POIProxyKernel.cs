using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;
using System.Threading;
using POIProxy.Handlers;

namespace POIProxy
{
    public class POIProxyKernel : POIKernel
    {
        POIComServer myDataHandler;

        public POISessionManager mySessionManager = new POISessionManager();
        public POIProxyPresCtrlHandler myPresCtrlHandler = new POIProxyPresCtrlHandler();
        public POIProxyPtrCtrlHandler myPtrCtrlHandler = new POIProxyPtrCtrlHandler();
        public POIProxyWBCtrlHandler myWBCtrlHandler = new POIProxyWBCtrlHandler();

        public Dictionary<string, POIUser> userCollection = new Dictionary<string, POIUser>();

        public void Start()
        {
            //Set the system kernel to connect with POI Communication lib
            POIGlobalVar.SystemKernel = this;
            POIGlobalVar.MaxMobileClientCount = 2000;
            
            //Publish the server address to the dns server
            POIWebService.StartService
            (
                @"Proxy testing",
                @"Taught by Prof. Gary Chan",
                @""
            );

            //Start the ComServer to handle the input data
            myDataHandler = new POIComServer(POIWebService.ServiceSocket);
            POIGlobalVar.SystemDataHandler = myDataHandler;
            POIGlobalVar.UserProfiles = userCollection;

            //Intialize the web user profiles
            POIGlobalVar.WebUserProfiles = new Dictionary<string, POIUser>();
            POIGlobalVar.WebConUserMap = new Dictionary<string, POIUser>();
        }

        #region Functions for the Kernel protocol

        public void HandleUserJoin(POIUser user) 
        {
            SetHandlersForUser(user);
        }

        public void SetHandlersForUser(POIUser user) 
        {
            /*
            user.PresCtrlHandler = new POIProxyPresCtrlHandler(user);

            POIProxyWBCtrlHandler handler = new POIProxyWBCtrlHandler(user);
            user.WhiteboardCtrlHandler = handler;
            user.CommentHandler = handler;

            user.SessionHandler = mySessionManager;
            user.PointerHandler = new POIProxyPtrCtrlHandler(user);
            user.AudioContentHandler = new POIProxyAudioContentHandler(user);*/

            //Use the shared handlers
            user.PresCtrlHandler = myPresCtrlHandler;
            user.WhiteboardCtrlHandler = myWBCtrlHandler;
            user.CommentHandler = myWBCtrlHandler;

            user.SessionHandler = mySessionManager;
            user.PointerHandler = myPtrCtrlHandler;
            user.AudioContentHandler = new POIProxyAudioContentHandler(user);
        }

        public void HandleUserLeave(POIUser user) 
        { 
            
        }

        #endregion

        //Handler functions for proxy the different messages



    }
}
