using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace POIProxy.Handlers
{
    public class POIProxyAudioContentHandler : POIAudioContentMsgCB
    {
        POIUser myUser;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int ConnectNamedPipe(
           SafeFileHandle hNamedPipe,
           IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeFileHandle CreateNamedPipe(
            String pipeName,
            uint dwOpenMode,
            uint dwPipeMode,
            uint nMaxInstances,
            uint nOutBufferSize,
            uint nInBufferSize,
            uint nDefaultTimeOut,
            IntPtr lpSecurityAttributes);

        public POIProxyAudioContentHandler(POIUser user)
        {
            myUser = user;
        }

        public void audioContentMsgReceived(POIAudioContentMsg msg)
        {
            //Write the audio into a pipe
        }
    }
}
