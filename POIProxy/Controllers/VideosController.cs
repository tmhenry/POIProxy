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
            string filePath = basePath + name;
            StreamReader reader = new StreamReader(filePath);

            var response = new HttpResponseMessage();
            var content = new StreamContent(reader.BaseStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
            response.Content = content;
            response.Headers.Add("Accept-Ranges", "bytes");

            return response;
        }
    }
}
