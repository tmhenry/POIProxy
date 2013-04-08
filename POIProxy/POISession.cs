using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;
using System.Threading;

using SignalR;
using SignalR.Hubs;
using POIProxy.SignalRFun;

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.IO;

namespace POIProxy
{
    public class POISession
    {
        public const uint DUPLEX = (0x00000003);
        public const uint FILE_FLAG_OVERLAPPED = (0x40000000);

        public string PIPE_NAME = "\\\\.\\pipe\\";
        public const uint BUFFER_SIZE = 4096;

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

        private SafeFileHandle streamingPipe;
        private List<POIUser> commanders;
        private List<POIUser> viewers;
        private int id;

        private POISessionPresController presController;
        private POISessionScheduler myScheduler = new POISessionScheduler();

        public SafeFileHandle StreamingPipe { get { return streamingPipe; } }
        public List<POIUser> Commanders { get { return commanders; } }
        public List<POIUser> Viewers { get { return viewers; } }
        public int Id { get { return id; } }
        public POISessionPresController PresController { get { return presController; } }
        public POISessionInfo Info { get; set; }

        private POIPresentation LoadPresFromContentServer(int contentId)
        {
            POIPresentation pres = new POIPresentation();
            int offset = 0;

            byte[] content = POIContentServerHelper.getPresInfo(contentId);

            if (content != null)
            {
                pres.deserialize(content, ref offset);
            }
            else
            {
                Console.WriteLine("Cannot get archive from content server!");
            }
            

            return pres;
        }

        public POISession(POIUser commander, int sessionId, int contentId)
        {
            //Load the presentation content according to the contentId
            POIPresentation presContent = LoadPresFromContentServer(contentId);
            
            //POIPresentation presContent = new POIPresentation();
            //presContent.LoadPresentationFromStorage();

            presController = new POISessionPresController(this);
            presController.LoadPresentation(presContent);

            Info = new POISessionInfo();
            Info.organization = @"Pipe of Insight";
            Info.presenterName = commander.UserID;
            Info.sessionId = sessionId;
            Info.contentId = contentId;

            id = sessionId;
            PIPE_NAME += id;

            commanders = new List<POIUser>();
            viewers = new List<POIUser>();
            JoinAsCommander(commander);


            Thread schedulerThread = new Thread(StartScheduler);
            schedulerThread.Name = @"SessionScheduler_" + id;
            schedulerThread.Start();

            //StartAudioStreamingService();
        }

        public void StartAudioStreamingService()
        {
            //Create a pipe and wait for the connection from ffmpeg
            streamingPipe = CreateNamedPipe(
                PIPE_NAME,
                DUPLEX | FILE_FLAG_OVERLAPPED,
                0,
                255,
                BUFFER_SIZE,
                BUFFER_SIZE,
                0,
                IntPtr.Zero
            );

            if (streamingPipe.IsInvalid)
                Console.WriteLine("Pipe creation failed!");

            //Listen on the ffmpeg to connect to the pipe
            Thread pipeListener = new Thread(ListenForStreamingServer);
            pipeListener.Name = @"Pipe Listener";
            pipeListener.Start();

            //Notify ffmpeg to connect and read the pipe
            NotifyAudioStreamingServer();
        }

        public void ListenForStreamingServer()
        {
            int success = ConnectNamedPipe(streamingPipe, IntPtr.Zero);
            if (success == -1)
            {
                Console.WriteLine("Pipe connection failed!");
            }
        }

        public void NotifyAudioStreamingServer()
        {
            //Start a cmd process which trigger ffmpeg
            Process process = new Process();

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";

            //Important: specify the options for ffmpeg
            //The PIPE_NAME is already defined so can be used here as input file
            //"/C" let the console close after operation completes
            startInfo.Arguments = "/C Test.exe";

            process.StartInfo = startInfo;
            process.Start();
        }

        public void StartScheduler()
        {
            myScheduler.Run();
        }

        public void JoinAsViewer(POIUser user)
        {
            if (!viewers.Contains(user))
            {
                viewers.Add(user);

                SendInitialSlides(user);
            }
        }

        public void JoinAsCommander(POIUser user)
        {
            if (!commanders.Contains(user))
            {
                commanders.Add(user);
                viewers.Add(user);

                SendInitialSlides(user);
            }
        }

        public void SendInitialSlides(POIUser user)
        {
            List<POISlide> myList = presController.GetInitialSlides();

            POIPresentation pres = new POIPresentation();
            for (int i = 0; i < myList.Count; i++)
            {
                pres.Insert(myList[i]);
            }

            myScheduler.ScheduleLowPriorityEvent
            (
                new POISessionPresSendEvent(user, pres)
            );
        }

        public void BroadcastPreloadSlide()
        {
            POIPresentation pres = new POIPresentation();
            POISlide slide = presController.GetPreloadSlide();

            if (slide != null)
            {
                pres.Insert(slide);

                for (int i = 0; i < viewers.Count; i++)
                {
                    myScheduler.ScheduleHighPriorityEvent
                    (
                        new POISessionPresSendEvent(viewers[i], pres)
                    );
                }

                //Broadcast the slide to all the web clients
                var context = GlobalHost.ConnectionManager.GetHubContext<POIProxyHub>();
                context.Clients[id.ToString()].getSlide(slide.Index);
            }
            else
            {
                Console.WriteLine("Preload slide is null!");
            }
        }

        public void LeaveAsViewer(POIUser user)
        {
            if (viewers.Contains(user))
                viewers.Remove(user);
        }

        public void LeaveAsCommander(POIUser user)
        {
            if (commanders.Contains(user))
                commanders.Remove(user);
        }

        public bool IsCommander(POIUser user)
        {
            return commanders.Contains(user);
        }

        public bool IsViewer(POIUser user)
        {
            return viewers.Contains(user);
        }

        public void SessionEnd()
        {

        }
    }

    public class POISessionInfo
    {
        public string presenterName;
        public string organization;
        public int sessionId;
        public int contentId;
    }
}
