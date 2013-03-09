using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using POILibCommunication;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace POIProxy.Handlers
{
    public class POIProxyAudioContentHandler : POIAudioContentMsgCB
    {
        POIUser myUser;

        public POIProxyAudioContentHandler(POIUser user)
        {
            myUser = user;
        }

        public void audioContentMsgReceived(POIAudioContentMsg msg)
        {
            //Get the session of the user
            var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
            var session = registery.GetSessionByUser(myUser);

            //Write the audio into a pipe
            FileStream os = new FileStream(session.StreamingPipe, FileAccess.Write, 4096);
            os.Write(msg.AudioBytes, 0, msg.AudioBytes.Length);
            os.Flush();
        }
    }
}
