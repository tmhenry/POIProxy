using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;

using POILibCommunication;
using Qiniu.IO;
using Qiniu.RS;
using System.Web.Configuration;

namespace POIProxy
{
    public class POICdnHelper
    {
        public static string generateCdnKeyForSessionArchive(string sessionId)
        {
            return "session_" + sessionId + "_" + POITimestamp.ConvertToUnixTimestamp(DateTime.Now);
        }

        public static string uploadStrToQiniuCDN(string key, string str)
        {
            try
            {
                //Save the string to a temp file
                string fileName = Path.GetTempFileName();
                using (StreamWriter outFile = new StreamWriter(fileName))
                {
                    outFile.Write(str);
                }

                POIGlobalVar.POIDebugLog("File is : " + fileName);

                //Upload the file to cdn
                return uploadFileToQiniuCDN(key, fileName);
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog(e.Message);
                return "";
            }
        }

        public static string uploadFileToQiniuCDN(string key, string fileName)
        {
            string bucket = WebConfigurationManager.AppSettings["QiniuBucket"];

            POIGlobalVar.POIDebugLog("Bucket is : " + bucket);
            var policy = new PutPolicy(bucket, 3600);

            string upToken = policy.Token();
            PutExtra extra = new PutExtra();
            extra.Crc32 = 1;

            IOClient client = new IOClient();
            PutRet ret = client.PutFile(upToken, key, fileName, extra);

            POIGlobalVar.POIDebugLog("Bucket is : " + bucket);
            POIGlobalVar.POIDebugLog("Key is : " + ret.key);
            POIGlobalVar.POIDebugLog("Response is : " + ret.Response);
            POIGlobalVar.POIDebugLog("Status code is : " + ret.StatusCode);
            POIGlobalVar.POIDebugLog("Hash is : " + ret.Hash);


            if (ret.OK)
            {
                POIGlobalVar.POIDebugLog("Upload successful: ");
            }
            else
            {
                POIGlobalVar.POIDebugLog("Cannot upload: ");
            }

            return ret.key;
        }
    }
}