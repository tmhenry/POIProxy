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
    public class ScriptsController : ApiController
    {
        private string basePath = @"../../Scripts/";

        public HttpResponseMessage GetScriptByName(string name)
        {
            string filePath = basePath + name;
            StreamReader reader = new StreamReader(filePath);
            //string fileString = reader.ReadToEnd();

            var response = new HttpResponseMessage();
            //response.Content = new StringContent(fileString, Encoding.UTF8, "text/javascript");
            response.Content = new StreamContent(reader.BaseStream);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/javascript");

            return response;
        }
    }
}
