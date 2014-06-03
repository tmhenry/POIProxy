using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;

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

                PPLog.infoLog("File is : " + fileName);

                //Upload the file to cdn
                return uploadFileToQiniuCDN(key, fileName);
            }
            catch (Exception e)
            {
                PPLog.errorLog(e.Message);
                return "";
            }
        }

        public static string uploadFileToQiniuCDN(string key, string fileName)
        {
            string bucket = WebConfigurationManager.AppSettings["QiniuBucket"];

            PPLog.infoLog("Bucket is : " + bucket);
            var policy = new PutPolicy(bucket, 3600);

            string upToken = policy.Token();
            PutExtra extra = new PutExtra();
            extra.Crc32 = 1;

            IOClient client = new IOClient();
            PutRet ret = client.PutFile(upToken, key, fileName, extra);

            PPLog.infoLog("Bucket is : " + bucket);
            PPLog.infoLog("Key is : " + ret.key);
            PPLog.infoLog("Response is : " + ret.Response);
            PPLog.infoLog("Status code is : " + ret.StatusCode);
            PPLog.infoLog("Hash is : " + ret.Hash);


            if (ret.OK)
            {
                PPLog.infoLog("Upload successful: ");
            }
            else
            {
                PPLog.infoLog("Cannot upload: ");
            }

            return ret.key;
        }
    }
}