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
    public class ImagesController : ApiController
    {
        private string basePath = @"../../Images/";

        public HttpResponseMessage GetImagesByName(string name)
        {
            string filePath = basePath + name;
            StreamReader reader = new StreamReader(filePath);
            
            var response = new HttpResponseMessage();
            var content = new StreamContent(reader.BaseStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            response.Content = content;
            
            return response;
        }
    }
}
