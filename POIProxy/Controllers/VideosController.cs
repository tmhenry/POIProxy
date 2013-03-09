using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Web.Http;
using System.Net.Http;
using System.Net.Http.Headers;

using System.IO;

namespace POIProxy.Controllers
{
    public class VideosController : ApiController
    {
        private string basePath = @"../../Videos/";

        public HttpResponseMessage GetVideosByName(string name)
        {
            Console.WriteLine(Request.Headers.Range);

            var response = new HttpResponseMessage();

            var requestRange = Request.Headers.Range;

            if (requestRange.Ranges.Count == 1)
            {
                RangeItemHeaderValue range = requestRange.Ranges.ElementAt(0);

                if (range.To == null) //Download the whole file
                {
                    string filePath = basePath + name;
                    StreamReader reader = new StreamReader(filePath);

                    var content = new StreamContent(reader.BaseStream);
                    content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                    response.Content = content;
                    response.Headers.Add("Accept-Ranges", "bytes");
                }
                else //Send a partial content
                {
                    string filePath = basePath + name;
                    //StreamReader reader = new StreamReader(filePath);
                    FileStream ins = File.OpenRead(filePath);
                    
                    long rangeLength = (long)(range.To - range.From);
                    long contentLength = ins.Length;

                    if (rangeLength == 0) return response;

                    byte[] buffer = new byte[rangeLength];
                    ins.Seek((long)range.From, 0);
                    for (int i = 0; i < rangeLength; i++)
                    {
                        buffer[i] = (byte)ins.ReadByte();
                    }
                    ins.Close();

                    //reader.BaseStream.Read(buffer, (int)range.From, (int) rangeLength);

                    MemoryStream stream = new MemoryStream(buffer);

                    var content = new StreamContent(stream);
                    content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                    response.Content = content;
                    response.Headers.Add("Accept-Ranges", "bytes");
                    response.StatusCode = System.Net.HttpStatusCode.PartialContent;

                    
                    response.Content.Headers.ContentRange = new ContentRangeHeaderValue
                    (
                        (long) range.From, 
                        (long) range.To, 
                        contentLength
                    );
                }

            }

            
            //response.Content.Headers.ContentRange = new ContentRangeHeaderValue()
            //response.StatusCode = System.Net.HttpStatusCode.PartialContent;

            return response;
        }
    }
}
