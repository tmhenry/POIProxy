using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using POILibCommunication;
using System.IO;

namespace POIProxy.Handlers
{
    public class POIProxyDataHandler: POIDataChannelMsgCB
    {
        public void pushMsgReceived(POIPushMsg msg, POIUser user)
        {
            //Handle the push message
            switch (msg.Type)
            {
                case (int)PushMsgType.Snapshot:
                    handleSnapshotMsgReceived(msg, user);
                    break;

                default:
                    POIGlobalVar.POIDebugLog("Unknown push msg type received!");
                    break;
            }
        }

        private void handleSnapshotMsgReceived(POIPushMsg msg, POIUser user)
        {
            //Get the presentation id and session id
            POISessionManager manager = POIProxyGlobalVar.Kernel.mySessionManager;
            POISession session = manager.Registery.GetSessionByUser(user);

            if (session != null)
            {
                int contentId = session.Info.contentId;
                int sessionId = session.Info.sessionId;

                try
                {
                    byte[] receivedData = msg.Data;
                    string snapshotIndex = msg.info[@"index"];

                    //Save the png to the local disk
                    string folderPath = Path.Combine(POIArchive.ArchiveHome, contentId.ToString());
                    Directory.CreateDirectory(folderPath);

                    string fileName = Path.Combine(folderPath, sessionId.ToString() + "_" + snapshotIndex + ".PNG");
                    FileStream writeStream = new FileStream(fileName, FileMode.OpenOrCreate);
                    BinaryWriter bw = new BinaryWriter(writeStream);

                    bw.Write(receivedData);

                    bw.Close();
                    writeStream.Close();

                    //Upload the content to the content server
                    POIContentServerHelper.uploadContent(contentId, fileName);
                }
                catch (Exception e)
                {
                    POIGlobalVar.POIDebugLog("Exception in uploading snapshot: " + e.Message);
                }
            }
            else
            {
                POIGlobalVar.POIDebugLog("Null session found for the user in handling data message!");
            }
        }
    }
}