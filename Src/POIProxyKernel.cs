using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web.Script.Serialization;

using System.Configuration;
using System.Web.Configuration;

using POILibCommunication;
using System.Threading;
using POIProxy.Handlers;
using log4net.Config;
using Qiniu.Conf;

namespace POIProxy
{
    public class POIProxyKernel : POIKernel
    {
        POIComServer myDataHandler;

        public POISessionManager mySessionManager = new POISessionManager();
        public POIProxyPresCtrlHandler myPresCtrlHandler = new POIProxyPresCtrlHandler();
        public POIProxyPtrCtrlHandler myPtrCtrlHandler = new POIProxyPtrCtrlHandler();
        public POIProxyWBCtrlHandler myWBCtrlHandler = new POIProxyWBCtrlHandler();
        public POIProxyDataHandler myDataMsgHandler = new POIProxyDataHandler();
        public POIProxyInteractiveMsgHandler myInterMsgHandler = new POIProxyInteractiveMsgHandler();
        

        public Dictionary<string, POIUser> userCollection = new Dictionary<string, POIUser>();
        private List<Timer> myActiveTimers = new List<Timer>();

        public void Start()
        {
            
            //Load the config file into the global definition
            loadConfigFile();

            //Register a log handler to enable web logging
            XmlConfigurator.Configure();
            POIGlobalVar.logDelegate = new POIProxyLogHandler();

            //Configure the qiniu storage
            //Qiniu.Conf.Config.Init();

            //Set the system kernel to connect with POI Communication lib
            POIGlobalVar.SystemKernel = this;
            POIGlobalVar.MaxMobileClientCount = 2000;
            /*
            //Initialize the buffer pool
            POITCPBufferPool.InitPool();
            
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
            */

            //Intialize the web user profiles
            POIGlobalVar.WebUserProfiles = new Dictionary<string, POIUser>();
            POIGlobalVar.WebConUserMap = new Dictionary<string, POIUser>();
            POIGlobalVar.InteractiveMsgUserMatchMap = new Dictionary<string, string>();
        }

        public void Stop()
        {
            try
            {
                
            }
            catch (Exception e)
            {

            }

        }

        public void Restart()
        {
            Stop();

            Start();
        }

        public void loadConfigFile()
        {
            //string fn = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"poi_config");

            try
            {
                POIGlobalVar.ContentServerHome = WebConfigurationManager.AppSettings["ContentServer"];
                POIGlobalVar.DNSServerHome = WebConfigurationManager.AppSettings["DNSServer"];
                POIGlobalVar.ProxyServerIP = WebConfigurationManager.AppSettings["ProxyServerIP"];
                POIGlobalVar.ProxyServerPort = Int32.Parse(WebConfigurationManager.AppSettings["ProxyServerPort"]);

                POIGlobalVar.ProxyHost = WebConfigurationManager.AppSettings["ProxyHost"];
                POIGlobalVar.ProxyPort = Int32.Parse(WebConfigurationManager.AppSettings["ProxyPort"]);

                POIGlobalVar.DbHost = WebConfigurationManager.AppSettings["DbHost"];
                POIGlobalVar.DbName = WebConfigurationManager.AppSettings["DbName"];
                POIGlobalVar.DbUsername = WebConfigurationManager.AppSettings["DbUsername"];
                POIGlobalVar.DbPassword = WebConfigurationManager.AppSettings["DbPassword"];

                //POIGlobalVar.POIDebugLog(POIGlobalVar.ContentServerHome);

            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog(e);
            }
        }

        public void updateConfig(Dictionary<string, string> configFields)
        {
            //Open the app config file
            Configuration config = WebConfigurationManager.OpenWebConfiguration("~");

            //Modify the config file as specified by the dictionary
            var section = config.AppSettings;
            if (section != null)
            {
                foreach (string key in configFields.Keys)
                {
                    try
                    {
                        POIGlobalVar.POIDebugLog(section.Settings[key].Value);
                        section.Settings[key].Value = configFields[key];
                    }
                    catch (Exception e)
                    {
                        POIGlobalVar.POIDebugLog(e);
                    }
                }
            }

            try
            {
                config.Save();
                loadConfigFile();
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog(e);
            }
            
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
            user.DataChannelMsgDelegate = myDataMsgHandler;
            
            user.SessionHandler = mySessionManager;
            user.PointerHandler = myPtrCtrlHandler;
            user.AudioContentHandler = new POIProxyAudioContentHandler(user);
        }

        public void HandleUserLeave(POIUser user) 
        { 
            //Set a timer to remove the user from the session registery
            //Remove the proper session as well
            //The timer is used for tolerating temporary networking failure
            Timer myTimer = null;

            TimerCallback tcb = new TimerCallback((userInfo) =>
            {
                //If the user is still disconnected, destroy the state
                POIUser curUser = userInfo as POIUser;
                double timeSinceLastConnected = (DateTime.UtcNow - curUser.LastConnected).TotalSeconds;

                if (curUser.Status == POIUser.ConnectionStatus.Disconnected && timeSinceLastConnected > 100)
                {
                    DestroyUserState(curUser);
                }
                    
                //remove the reference to the timer
                myActiveTimers.Remove(myTimer);

                myTimer.Dispose();
            });

            myTimer = new Timer(
                tcb,
                user, 
                100000, 
                System.Threading.Timeout.Infinite
            );

            //Keep the reference to the timer to avoid garbage collection
            myActiveTimers.Add(myTimer);
        }

        private void DestroyUserState(POIUser user)
        {
            //End the associated session if any
            mySessionManager.EndSession(user);

            //Remove the user from the user profile
            try
            {
                
                userCollection.Remove(user.UserID);
            }
            catch
            {
                POIGlobalVar.POIDebugLog("Cannot remove user from profiles!");
            }
        }

        #endregion

        //Handler functions for proxy the different messages



    }
}
